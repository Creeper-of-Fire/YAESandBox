using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Runtime.InstanceId;
using YAESandBox.Workflow.Runtime.Processor.RuneProcessor;

namespace YAESandBox.Workflow.Runtime.Processor;

// tuum的信息：
// 使用的脚本符文们的UUID（注意，脚本符文本身就是绑定在枢机上的，如果需要把符文复制到更广的地方，可以考虑直接复制枢机之类的）
/// <summary>
/// 枢机配置的运行时
/// </summary>
public class TuumProcessor(
    TuumConfig config,
    ICreatingContext creatingContext
) : IProcessorWithDebugDto<ITuumProcessorDebugDto>
{
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger<TuumProcessor>();

    /// <summary>
    /// 配置对象
    /// </summary>
    public TuumConfig Config { get; } = config;

    /// <summary>
    /// 枢机的运行时上下文
    /// </summary>
    public ProcessorContext ProcessorContext { get; } = creatingContext.ExtractContext();

    /// <summary>
    /// 枢机提供给内部符文的上下文/内部运行时
    /// </summary>
    public TuumProcessorContent TuumContent { get; } = new(config, creatingContext.ExtractContext());

    /// <summary>
    /// 枢机运行时的上下文
    /// </summary>
    public class TuumProcessorContent(TuumConfig tuumConfig, ProcessorContext processorContext)
    {
        /// <summary>
        /// 枢机的内部变量池
        /// </summary>
        public Dictionary<string, object?> TuumVariable { get; } = [];

        /// <summary>
        /// 得到枢机的变量
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public object? GetTuumVar(string name)
        {
            return this.TuumVariable.GetValueOrDefault(name);
        }

        /// <summary>
        /// 设置枢机的变量
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetTuumVar(string name, object? value)
        {
            this.TuumVariable[name] = value;
        }

        /// <summary>
        /// 枢机的配置
        /// </summary>
        public TuumConfig TuumConfig { get; } = tuumConfig;

        /// <summary>
        /// 工作流的运行时服务
        /// </summary>
        public ProcessorContext ProcessorContext { get; } = processorContext;

        /// <summary>
        /// 获得枢机的变量，带有类型转换，并且有序列化尝试
        /// </summary>
        public T? GetTuumVar<T>(string valueName)
        {
            if (this.TryGetTuumVar<T>(valueName, out var value))
            {
                return value;
            }

            return default;
        }

        /// <summary>
        /// 尝试获得枢机的变量，带有类型转换，并且有序列化尝试。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="valueName">变量名。</param>
        /// <param name="value">如果成功，则输出转换后的值；否则为 null 或 default。</param>
        /// <returns>如果成功找到并转换了变量，则返回 true；否则返回 false。</returns>
        public bool TryGetTuumVar<T>(string valueName, [MaybeNullWhen(false)] out T value)
        {
            object? rawValue = this.GetTuumVar(valueName);

            try
            {
                if (TryConvertValue(rawValue, out value))
                {
                    // 转换成功！为了后续访问效率，将转换后的结果写回 TuumVariable
                    // （仅当值发生实际变化时才写回，避免不必要的字典操作）
                    if (!ReferenceEquals(rawValue, value) && rawValue is not (JsonElement or JsonNode))
                    {
                        this.SetTuumVar(valueName, value);
                    }

                    return true;
                }

                // 转换失败
                value = default;
                return false;
            }
            catch (Exception ex)
            {
                value = default;
                Logger.Error(ex,
                    "无法将值'{ValueName}'(类型: {ObjectFullName})转换为 {TFullName}：JSON 转换失败。",
                    valueName, rawValue?.GetType().FullName, typeof(T).FullName);
                return false;
            }
        }

        /// <summary>
        /// 将任意对象安全地转换为目标类型 T 的静态辅助方法。
        /// </summary>
        /// <exception cref="InvalidCastException"> 转换失败。</exception>
        private static bool TryConvertValue<T>(object? rawValue, [MaybeNullWhen(false)] out T value)
        {
            // Case 1: 变量不存在或其值就是 null。
            if (rawValue is null)
            {
                value = default;
                return false;
            }

            // Case 2: 变量已经是正确的类型（最快路径）。
            if (rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            try
            {
                var result = rawValue switch
                {
                    // Case 3: 变量是从持久化存储中恢复的 JsonElement。
                    JsonElement jsonElement => jsonElement.Deserialize<T>(YaeSandBoxJsonHelper.JsonSerializerOptions),
                    JsonNode jsonNode => jsonNode.Deserialize<T>(YaeSandBoxJsonHelper.JsonSerializerOptions),
                    // Case 4: 变量是一个字符串，可能是一个JSON字符串。
                    // 如果目标类型本身就是字符串，Case 2 已经处理过了。
                    // 所以到这里，我们假设这个字符串是需要被解析的JSON。
                    string stringValue => JsonSerializer.Deserialize<T>(stringValue, YaeSandBoxJsonHelper.JsonSerializerOptions),
                    // Case 5: 最后的备用方案。
                    // 变量是其他类型的C#对象 (例如 MyRecordA)，需要转换为另一种类型 (例如 MyRecordB)。
                    // 这是通过序列化到JSON再反序列化来实现的，开销最大，应作为最后手段。
                    _ => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(rawValue, YaeSandBoxJsonHelper.JsonSerializerOptions),
                        YaeSandBoxJsonHelper.JsonSerializerOptions)
                };

                // 检查反序列化结果。
                // 注意：仅检查null，因为 default(T) 可能是有效值 (如 0 或 false)。
                if (result is null)
                {
                    value = result;
                    return false;
                }

                value = result;
                return true;
            }
            catch (InvalidCastException)
            {
                value = default;
                throw;
            }
        }

        /// <summary>
        /// 尝试通过点符号路径 (e.g., "player.stats.level") 获取枢机中的变量。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="path">变量路径。</param>
        /// <param name="value">如果成功，则输出转换后的值。</param>
        /// <returns>如果成功找到并转换了变量，则返回 true。</returns>
        public bool TryGetTuumVarByPath<T>(string path, [MaybeNullWhen(false)] out T value)
        {
            string[] parts = path.Split('.');
            if (parts.Length == 0)
            {
                value = default;
                return false;
            }

            // 1. 获取根对象
            if (!this.TryGetTuumVar<object>(parts[0], out object? currentObject))
            {
                value = default;
                return false;
            }

            // 2. 遍历路径的其余部分
            for (int i = 1; i < parts.Length; i++)
            {
                if (currentObject is null)
                {
                    value = default;
                    return false;
                }

                string key = parts[i];

                switch (currentObject)
                {
                    case IDictionary<string, object> dict:
                        if (!dict.TryGetValue(key, out currentObject))
                        {
                            value = default;
                            return false;
                        }

                        break;

                    case JsonElement { ValueKind: JsonValueKind.Object } jsonElement:
                        if (!jsonElement.TryGetProperty(key, out var nextElement))
                        {
                            value = default;
                            return false;
                        }

                        currentObject = nextElement;
                        break;

                    // 兜底方案：使用反射处理 POCO/record 类型
                    default:
                        var prop = currentObject.GetType().GetProperty(key,
                            System.Reflection.BindingFlags.IgnoreCase |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Instance);

                        if (prop == null)
                        {
                            value = default;
                            return false;
                        }

                        currentObject = prop.GetValue(currentObject);
                        break;
                }
            }

            // 3. 对最终找到的对象调用统一的转换逻辑
            try
            {
                return TryConvertValue(currentObject, out value);
            }
            catch (Exception ex)
            {
                value = default;
                Logger.Error(ex,
                    "无法将地址'{Path}'(类型: {ObjectFullName})转换为 {TFullName}：JSON 转换失败。",
                    path, currentObject?.GetType().FullName, typeof(T).FullName);
                return false;
            }
        }
    }

    /// <summary>
    /// 此枢机声明的所有输入端点的名称。
    /// </summary>
    internal IEnumerable<string> InputEndpoints { get; } = config.InputMappings.Values;

    /// <summary>
    /// 此枢机声明的所有输出端点的名称。
    /// </summary>
    internal IEnumerable<string> OutputEndpoints { get; } = config.OutputMappings.Values.SelectMany(v => v).Distinct();

    /// <summary>
    /// 启动枢机流程。
    /// </summary>
    /// <param name="inputs">一个字典，Key是输入端点的名称，Value是输入的数据。</param>
    /// <param name="cancellationToken"></param>
    /// <returns>一个包含此枢机所有输出的字典，Key是输出端点的名称。</returns>
    public async Task<Result<Dictionary<string, object?>>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken = default)
    {
        var persistenceService = this.ProcessorContext.RuntimeService.PersistenceService;
        var tuumInstanceId = this.ProcessorContext.InstanceId;

        // Tuum 的输出是一个字典，它不应该为 null，所以使用 ExecuteNonNullAsync
        return await persistenceService.WithPersistence(tuumInstanceId, inputs).ExecuteAsync(async currentInputs =>
        {
            // 1. 根据输入端点的数据，填充枢机的内部变量池
            // 遍历新的输入映射: "内部变量名" -> "外部端点名"
            // 这个循环的每一次迭代都只处理一个内部变量和它的唯一数据源。
            // 注意：我们使用 currentInputs，它可能是从持久化中恢复的
            foreach ((string internalName, string endpointName) in this.Config.InputMappings)
            {
                // 从工作流提供的总输入中，为当前内部变量查找其数据源 (外部端点) 的值。
                // 如果上游没有提供数据，endpointValue 将为 null。
                // 校验阶段应确保所有必要的输入都已被连接，以避免此处出现非预期的 null。
                currentInputs.TryGetValue(endpointName, out object? endpointValue);

                // 直接将获取到的值赋给对应的内部变量。
                // 逻辑非常清晰：一个内部变量，一个赋值操作。
                this.TuumContent.SetTuumVar(internalName, endpointValue);
            }

            // 2. 依次执行所有符文
            var runesExecutionResult = await this.ExecuteRuneSequenceWithLoggingAsync(cancellationToken);
            // 如果符文序列执行失败，则 Tuum 失败
            if (runesExecutionResult.TryGetError(out var runesExecutionError))
            {
                return runesExecutionError;
            }

            // 3. 根据输出映射，从内部变量池收集所有输出
            var tuumOutputs = new Dictionary<string, object?>();
            // 遍历输出映射: "内部变量名" -> [ "外部端点名1", "外部端点名2", ... ]
            foreach ((string internalName, var endpointNames) in this.Config.OutputMappings)
            {
                // 从内部变量池中查找由符文产生的局部变量的值
                object? internalValue = this.TuumContent.TuumVariable.GetValueOrDefault(internalName);

                // 将这个内部变量的值赋给所有映射到的外部输出端点 (Fan-out)
                foreach (string endpointName in endpointNames)
                {
                    tuumOutputs[endpointName] = internalValue;
                }
            }

            return Result.Ok(tuumOutputs);
        }).RunAsync();
    }

    /// <summary>
    /// 按顺序执行 Tuum 内的所有符文，并记录每个符文的执行细节。
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>如果所有符文都成功，则返回 Result.Ok()；否则返回第一个失败符文的 Error。</returns>
    private async Task<Result> ExecuteRuneSequenceWithLoggingAsync(CancellationToken cancellationToken)
    {
        foreach (var runeConfig in this.Config.Runes)
        {
            if (!runeConfig.Enabled)
                continue;

            string runeConfigId = runeConfig.ConfigId;
            var runeCreatingContext = this.ProcessorContext.CreateChildWithScope(runeConfigId);
            var runeProcessor = runeConfig.ToRuneProcessor(runeCreatingContext);

            var stopwatch = Stopwatch.StartNew();
            var result = Result.Ok();
            try
            {
                // (仅处理INormalRune，未来可扩展)
                if (runeProcessor is INormalRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto> normalRune)
                {
                    // Rune 自身可能会使用持久化，也可能不使用。
                    // TuumProcessor 对此不知情，它只负责调用和等待结果。
                    result = await normalRune.ExecuteAsync(this.TuumContent, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // 捕获未预期的异常，确保日志记录和流程控制
                result = Result.Fail(new Error("执行符文时发生未捕获的异常。", ex));
            }
            finally
            {
                stopwatch.Stop();
                // 无论成功或失败，都调用专门的函数来记录符文的执行细节
                this.LogRuneExecutionDetails(runeProcessor, result, stopwatch.Elapsed);
            }

            if (result.TryGetError(out var error))
                return error; // 如果任何一个符文失败，整个枢机失败
        }

        return Result.Ok();
    }


    /// <summary>
    /// 记录单个符文执行后的详细信息，包括其状态、元数据和完整的DebugDto快照。
    /// </summary>
    /// <param name="rune">执行的符文实例。</param>
    /// <param name="result">符文的执行结果。</param>
    /// <param name="duration">执行耗时。</param>
    private void LogRuneExecutionDetails(
        IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto> rune,
        Result result,
        TimeSpan duration)
    {
        var config = rune.Config;
        string status = result.IsSuccess ? "成功" : "失败";

        if (result.IsSuccess)
        {
            Logger.Info(
                "符文执行 | 状态: {Status}, 名称: {RuneName}, 类型: {RuneType}, ID: {RuneId}, 耗时: {DurationMs:F2}ms | Debug信息: {@DebugDto}",
                status,
                config.Name,
                config.RuneType,
                config.ConfigId,
                duration.TotalMilliseconds,
                rune.DebugDto
            );
        }
        else
        {
            // 对于失败的符文，将异常/错误对象作为第一个参数
            // 结构化日志库会自动处理它
            Logger.Error(
                result.ErrorException, // 底层的错误/异常对象
                "符文执行 | 状态: {Status}, 名称: {RuneName}, 类型: {RuneType}, ID: {RuneId}, 耗时: {DurationMs:F2}ms | Debug信息: {@DebugDto}",
                status,
                config.Name,
                config.RuneType,
                config.ConfigId,
                duration.TotalMilliseconds,
                rune.DebugDto
            );
        }
    }


    /// <inheritdoc />
    public ITuumProcessorDebugDto DebugDto => new TuumProcessorDebugDto
        { RuneProcessorDebugDtos = [] };
    // { RuneProcessorDebugDtos = this.Runes.ConvertAll(it => it.DebugDto) };
    // TODO 目前我们的debugDto是直接打印到后台的，暂时不需要它，而且我们考虑之后引入一个新的横切关注点来实现它，而不是透传。

    /// <inheritdoc />
    internal record TuumProcessorDebugDto : ITuumProcessorDebugDto
    {
        /// <inheritdoc />
        public required IList<IRuneProcessorDebugDto> RuneProcessorDebugDtos { get; init; }
    }
}