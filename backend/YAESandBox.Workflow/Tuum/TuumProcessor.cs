using System.Text.Json;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune;
using static YAESandBox.Workflow.Core.WorkflowProcessor;

namespace YAESandBox.Workflow.Tuum;

//tuum的信息：
// 使用的脚本符文们的UUID（注意，脚本符文本身就是绑定在祝祷上的，如果需要把符文复制到更广的地方，可以考虑直接复制祝祷之类的）
/// <summary>
/// 祝祷配置的运行时
/// </summary>
public class TuumProcessor(
    WorkflowRuntimeService workflowRuntimeService,
    TuumConfig config)
    : IProcessorWithDebugDto<ITuumProcessorDebugDto>
{
    private TuumConfig Config { get; } = config;

    /// <summary>
    /// 祝祷的上下文/内部运行时
    /// </summary>
    public TuumProcessorContent TuumContent { get; } = new(config, workflowRuntimeService);

    /// <summary>
    /// 祝祷运行时的上下文
    /// </summary>
    public class TuumProcessorContent(TuumConfig tuumConfig, WorkflowRuntimeService workflowRuntimeService)
    {
        /// <summary>
        /// 祝祷的内部变量池
        /// </summary>
        public Dictionary<string, object?> TuumVariable { get; } = [];

        /// <summary>
        /// 得到祝祷的变量
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public object? GetTuumVar(string name)
        {
            return this.TuumVariable.GetValueOrDefault(name);
        }

        /// <summary>
        /// 设置祝祷的变量
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetTuumVar(string name, object? value)
        {
            this.TuumVariable[name] = value;
        }

        /// <summary>
        /// 祝祷的配置
        /// </summary>
        public TuumConfig TuumConfig { get; } = tuumConfig;

        /// <summary>
        /// 工作流的运行时服务
        /// </summary>
        public WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

        /// <summary>
        /// 获得祝祷的变量，带有类型转换，并且有序列化尝试
        /// </summary>
        /// <param name="valueName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidCastException"></exception>
        public T? GetTuumVar<T>(string valueName)
        {
            object? tryGetValue = this.GetTuumVar(valueName);
            if (tryGetValue is null || tryGetValue.Equals(default(T)))
                return default;

            if (tryGetValue is T promptsValue)
                return promptsValue;

            try
            {
                // 将 C# 对象序列化成 JSON 字符串
                string json = JsonSerializer.Serialize(tryGetValue);

                var result = JsonSerializer.Deserialize<T>(json, YaeSandBoxJsonHelper.JsonSerializerOptions);

                if (result is null || result.Equals(default(T)))
                    return default;

                // 为了后续访问效率，可以将转换后的结果写回 TuumVariable
                this.SetTuumVar(valueName, result);

                return result;
            }
            catch (Exception ex)
            {
                return default;
                // 提供详细的错误信息
                // TODO 应该记录在Tuum的Debug里面

                // throw new InvalidCastException(
                // $"无法将值'{valueName}'(类型: {tryGetValue.GetType().FullName})转换为 {typeof(T).FullName}。JSON 转换失败: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// 此祝祷声明的所有输入端点的名称。
    /// </summary>
    internal IEnumerable<string> InputEndpoints { get; } = config.InputMappings.Values.Distinct();

    /// <summary>
    /// 此祝祷声明的所有输出端点的名称。
    /// </summary>
    internal IEnumerable<string> OutputEndpoints { get; } = config.OutputMappings.Keys;

    private List<IProcessorWithDebugDto<IRuneProcessorDebugDto>> Runes { get; } =
        config.Runes.Select(rune => rune.ToRuneProcessor(workflowRuntimeService)).ToList();

    /// <summary>
    /// 启动祝祷流程。
    /// </summary>
    /// <param name="inputs">一个字典，Key是输入端点的名称，Value是输入的数据。</param>
    /// <param name="cancellationToken"></param>
    /// <returns>一个包含此祝祷所有输出的字典，Key是输出端点的名称。</returns>
    public async Task<Result<Dictionary<string, object?>>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken = default)
    {
        // 1. 根据输入端点的数据，填充祝祷的内部变量池
        foreach ((string internalName, string endpointName) in this.Config.InputMappings)
        {
            // 如果输入端点没有提供数据（可能因连接或上游问题），则内部变量为 null
            // 理论上，WorkflowProcessor应该确保所有连接的输入都被提供。
            // 如果到这里还找不到，说明上游逻辑有误。
            this.TuumContent.SetTuumVar(internalName, inputs.TryGetValue(endpointName, out object? value) ? value : null);
        }

        // 2. 依次执行所有符文
        foreach (var rune in this.Runes)
        {
            // (仅处理INormalRune，未来可扩展)
            if (rune is INormalRune normalRune)
            {
                var result = await normalRune.ExecuteAsync(this.TuumContent, cancellationToken);
                if (result.TryGetError(out var error))
                    return error; // 如果任何一个符文失败，整个祝祷失败
            }
        }

        // 3. 根据输出映射，从内部变量池收集所有输出
        var tuumOutputs = new Dictionary<string, object?>();
        foreach ((string endpointName, string internalName) in this.Config.OutputMappings)
        {
            // 从内部变量池中查找由符文产生的局部变量
            if (this.TuumContent.TuumVariable.TryGetValue(internalName, out object? internalValue))
            {
                tuumOutputs[endpointName] = internalValue;
            }
            else
            {
                // 如果映射声明了，但符文未产生输出，则该输出端点的值为 null
                tuumOutputs[endpointName] = null;
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