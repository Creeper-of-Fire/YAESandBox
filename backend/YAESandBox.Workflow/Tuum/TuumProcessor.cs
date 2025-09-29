using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune;

namespace YAESandBox.Workflow.Tuum;

//tuum的信息：
// 使用的脚本符文们的UUID（注意，脚本符文本身就是绑定在枢机上的，如果需要把符文复制到更广的地方，可以考虑直接复制枢机之类的）
/// <summary>
/// 枢机配置的运行时
/// </summary>
public class TuumProcessor(
    WorkflowRuntimeService workflowRuntimeService,
    TuumConfig config)
    : IProcessorWithDebugDto<ITuumProcessorDebugDto>
{
    private TuumConfig Config { get; } = config;

    /// <summary>
    /// 枢机的上下文/内部运行时
    /// </summary>
    public TuumProcessorContent TuumContent { get; } = new(config, workflowRuntimeService);

    /// <summary>
    /// 枢机运行时的上下文
    /// </summary>
    public class TuumProcessorContent(TuumConfig tuumConfig, WorkflowRuntimeService workflowRuntimeService)
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
        public WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

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
                string json;
                if (rawValue is string stringTryGetValue)
                    json = stringTryGetValue;
                else
                    // 将 C# 对象序列化成 JSON 字符串
                    json = JsonSerializer.Serialize(rawValue);

                var result = JsonSerializer.Deserialize<T>(json, YaeSandBoxJsonHelper.JsonSerializerOptions);

                if (result is null || result.Equals(default(T)))
                {
                    value = result;
                    return false;
                }

                // 为了后续访问效率，可以将转换后的结果写回 TuumVariable
                this.SetTuumVar(valueName, result);
                value = result;
                return true;
            }
            catch (Exception ex)
            {
                value = default;
                throw new InvalidCastException(
                    $"无法将值'{valueName}'(类型: {rawValue.GetType().FullName})转换为 {typeof(T).FullName}：JSON 转换失败。", ex);
            }
        }
    }

    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

    /// <summary>
    /// 此枢机声明的所有输入端点的名称。
    /// </summary>
    internal IEnumerable<string> InputEndpoints { get; } = config.InputMappings.Values;

    /// <summary>
    /// 此枢机声明的所有输出端点的名称。
    /// </summary>
    internal IEnumerable<string> OutputEndpoints { get; } = config.OutputMappings.Values.SelectMany(v => v).Distinct();

    private List<IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto>> Runes { get; } =
        config.Runes.Select(rune => rune.ToRuneProcessor(workflowRuntimeService)).ToList();

    // 缓存所有Rune的运行时实例，以ConfigId为键
    private Dictionary<string, IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto>> RuneProcessors { get; } =
        config.Runes.ToDictionary(
            runeConfig => runeConfig.ConfigId,
            runeConfig => runeConfig.ToRuneProcessor(workflowRuntimeService)
        );

    /// <summary>
    /// 启动枢机流程。
    /// </summary>
    /// <param name="inputs">一个字典，Key是输入端点的名称，Value是输入的数据。</param>
    /// <param name="cancellationToken"></param>
    /// <returns>一个包含此枢机所有输出的字典，Key是输出端点的名称。</returns>
    public async Task<Result<Dictionary<string, object?>>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken = default)
    {
        // 1. 根据输入端点的数据，填充枢机的内部变量池
        // 遍历新的输入映射: "内部变量名" -> "外部端点名"
        // 这个循环的每一次迭代都只处理一个内部变量和它的唯一数据源。
        foreach ((string internalName, string endpointName) in this.Config.InputMappings)
        {
            // 从工作流提供的总输入中，为当前内部变量查找其数据源 (外部端点) 的值。
            // 如果上游没有提供数据，endpointValue 将为 null。
            // 校验阶段应确保所有必要的输入都已被连接，以避免此处出现非预期的 null。
            inputs.TryGetValue(endpointName, out object? endpointValue);

            // 直接将获取到的值赋给对应的内部变量。
            // 逻辑非常清晰：一个内部变量，一个赋值操作。
            this.TuumContent.SetTuumVar(internalName, endpointValue);
        }

        // 2. 依次执行所有符文
        foreach (var rune in this.Runes)
        {
            if (!rune.Config.Enabled)
                continue;
            // (仅处理INormalRune，未来可扩展)
            if (rune is INormalRune<AbstractRuneConfig, IRuneProcessorDebugDto> normalRune)
            {
                var result = await normalRune.ExecuteAsync(this.TuumContent, cancellationToken);
                if (result.TryGetError(out var error))
                    return error; // 如果任何一个符文失败，整个枢机失败
            }
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

        return tuumOutputs;
    }


    /// <inheritdoc />
    public ITuumProcessorDebugDto DebugDto => new TuumProcessorDebugDto
        { RuneProcessorDebugDtos = this.Runes.ConvertAll(it => it.DebugDto) };

    /// <inheritdoc />
    internal record TuumProcessorDebugDto : ITuumProcessorDebugDto
    {
        /// <inheritdoc />
        public required IList<IRuneProcessorDebugDto> RuneProcessorDebugDtos { get; init; }
    }
}