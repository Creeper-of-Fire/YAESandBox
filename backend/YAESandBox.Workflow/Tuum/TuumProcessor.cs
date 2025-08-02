using System.Text.Json;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune;
using static YAESandBox.Workflow.WorkflowProcessor;

namespace YAESandBox.Workflow.Tuum;

//tuum的信息：
// 使用的脚本符文们的UUID（注意，脚本符文本身就是绑定在祝祷上的，如果需要把符文复制到更广的地方，可以考虑直接复制祝祷之类的）
/// <summary>
/// 祝祷配置的运行时
/// </summary>
public class TuumProcessor(
    WorkflowRuntimeService workflowRuntimeService,
    TuumProcessorConfig config)
    : IWithDebugDto<ITuumProcessorDebugDto>
{
    internal TuumProcessorConfig Config { get; } = config;
    internal TuumProcessorContent TuumContent { get; } = new(config, workflowRuntimeService);

    /// <summary>
    /// 祝祷运行时的上下文
    /// </summary>
    public class TuumProcessorContent(TuumProcessorConfig tuumProcessorConfig, WorkflowRuntimeService workflowRuntimeService)
    {
        public Dictionary<string, object?> TuumVariable { get; } = [];

        public object? InputVar(string name)
        {
            return this.TuumVariable.GetValueOrDefault(name);
        }

        public void OutputVar(string name, object? value)
        {
            this.TuumVariable[name] = value;
        }

        public TuumProcessorConfig TuumProcessorConfig { get; } = tuumProcessorConfig;

        public WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

        public IList<RoledPromptDto> Prompts
        {
            get
            {
                if (!this.TuumVariable.TryGetValue(nameof(this.Prompts), out object? value))
                    return new List<RoledPromptDto>();
                if (value is IList<RoledPromptDto> prompts)
                    return prompts;

                try
                {
                    // 将从 Lua 返回的 C# 对象（如 List<object>）序列化成 JSON 字符串
                    string json = JsonSerializer.Serialize(value);

                    var result = JsonSerializer.Deserialize<List<RoledPromptDto>>(json, YaeSandBoxJsonHelper.JsonSerializerOptions);

                    if (result == null)
                    {
                        // 如果反序列化结果是 null (例如，输入是 "null" 字符串)，返回空列表
                        return new List<RoledPromptDto>();
                    }

                    // 为了后续访问效率，可以将转换后的结果写回 TuumVariable
                    this.TuumVariable[nameof(this.Prompts)] = result;

                    return result;
                }
                catch (Exception ex)
                {
                    // 提供详细的错误信息
                    throw new InvalidCastException(
                        $"无法将 TuumVariable中的'Prompts'值(类型: {value.GetType().FullName})转换为 IList<RoledPromptDto>。JSON 转换失败: {ex.Message}",
                        ex);
                }
            }
        }
    }

    /// <summary>
    /// 消费者（Consumes）：此祝祷需要从全局变量池中获取的所有变量的【全局名称】。
    /// 在严格模式下，这个集合就是 InputMappings 的所有 Value。
    /// </summary>
    internal IEnumerable<string> GlobalConsumers { get; } = config.InputMappings.Values;

    /// <summary>
    /// 生产者（Produces）：此祝祷通过 OutputMappings 向全局变量池声明输出的变量。
    /// </summary>
    internal IEnumerable<string> GlobalProducers { get; } = config.OutputMappings.Keys;

    private List<IWithDebugDto<IRuneProcessorDebugDto>> Runes { get; } =
        config.Runes.Select(rune => rune.ToRuneProcessor(workflowRuntimeService)).ToList();
    
    internal WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

    /// <summary>
    /// 启动祝祷流程
    /// </summary>
    /// <param name="workflowRuntimeContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Result<Dictionary<string, object>>> ExecuteTuumsAsync(
        WorkflowRuntimeContext workflowRuntimeContext, CancellationToken cancellationToken = default)
    {
        // 严格根据 InputMappings 从全局变量池填充祝祷的内部变量池
        foreach ((string localName, string globalName) in this.Config.InputMappings)
        {
            if (!workflowRuntimeContext.GlobalVariables.TryGetValue(globalName, out object? value))
            {
                // 这一层校验理论上在静态分析时已完成，但在这里，“直接获取然后失败时抛错误”和“失败时返回Result”是一样的。
                return NormalError.Conflict($"执行祝祷 '{this.Config.ConfigId}' 失败：找不到必需的全局输入变量 '{globalName}'。");
            }

            this.TuumContent.TuumVariable[localName] = value;
        }

        // this.TuumContent.TuumVariable[nameof(WorkflowRuntimeContext.FinalRawText)] = workflowRuntimeContext.FinalRawText;
        // this.TuumContent.TuumVariable[nameof(WorkflowRuntimeContext.GeneratedOperations)] = workflowRuntimeContext.GeneratedOperations;

        foreach (var rune in this.Runes)
        {
            switch (rune)
            {
                case INormalRune normalRune:
                    var result = await normalRune.ExecuteAsync(this.TuumContent, cancellationToken);
                    if (result.TryGetError(out var error))
                        return error;
                    break;
            }
        }

        var tuumOutput = new Dictionary<string, object>();

        // if (this.TuumContent.TuumVariable.TryGetValue(nameof(WorkflowRuntimeContext.FinalRawText), out object? finalRawText))
        // {
        //     workflowRuntimeContext.FinalRawText = (string)finalRawText;
        // }
        //
        // if (this.TuumContent.TuumVariable.TryGetValue(nameof(WorkflowRuntimeContext.GeneratedOperations), out object? generatedOperations))
        // {
        //     workflowRuntimeContext.GeneratedOperations = (List<AtomicOperation>)generatedOperations;
        // }

        foreach ((string globalName, string localName) in this.Config.OutputMappings)
        {
            // 从本祝祷的内部变量池中查找由符文产生的局部变量
            if (this.TuumContent.TuumVariable.TryGetValue(localName, out object? localValue))
            {
                tuumOutput[globalName] = localValue;
            }
            // else 
            // {
            //   可选：在这里可以处理映射声明了，但符文实际并未产生输出的情况
            //   例如：记录一个警告日志，或者根据严格模式抛出异常
            //   根据“后端不验证”的原则，我们暂时忽略这种情况
            // }
        }

        return tuumOutput;
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