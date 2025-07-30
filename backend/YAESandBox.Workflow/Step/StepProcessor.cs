using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Module;
using static YAESandBox.Workflow.WorkflowProcessor;

namespace YAESandBox.Workflow.Step;

//step的信息：
// 使用的脚本模块们的UUID（注意，脚本模块本身就是绑定在步骤上的，如果需要把模块复制到更广的地方，可以考虑直接复制步骤之类的）
/// <summary>
/// 步骤配置的运行时
/// </summary>
internal class StepProcessor(
    WorkflowRuntimeService workflowRuntimeService,
    StepProcessorConfig config)
    : IWithDebugDto<IStepProcessorDebugDto>
{
    internal StepProcessorConfig Config { get; } = config;
    internal StepProcessorContent StepContent { get; } = new(config, workflowRuntimeService);

    /// <summary>
    /// 步骤运行时的上下文
    /// </summary>
    public class StepProcessorContent(StepProcessorConfig stepProcessorConfig, WorkflowRuntimeService workflowRuntimeService)
    {
        public Dictionary<string, object> StepVariable { get; } = [];

        public object? InputVar(string name)
        {
            return this.StepVariable.GetValueOrDefault(name);
        }

        public void OutputVar(string name, object value)
        {
            this.StepVariable[name] = value;
        }

        public StepProcessorConfig StepProcessorConfig { get; } = stepProcessorConfig;

        public WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;
    }

    /// <summary>
    /// 消费者（Consumes）：此步骤需要从全局变量池中获取的所有变量的【全局名称】。
    /// 在严格模式下，这个集合就是 InputMappings 的所有 Value。
    /// </summary>
    internal IEnumerable<string> GlobalConsumers { get; } = config.InputMappings.Values;

    /// <summary>
    /// 生产者（Produces）：此步骤通过 OutputMappings 向全局变量池声明输出的变量。
    /// </summary>
    internal IEnumerable<string> GlobalProducers { get; } = config.OutputMappings.Keys;

    private List<IWithDebugDto<IModuleProcessorDebugDto>> Modules { get; } =
        config.Modules.Select(module => module.ToModuleProcessor(workflowRuntimeService)).ToList();

    internal StepAiConfig? StepAiConfig { get; } = config.StepAiConfig;
    internal WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

    /// <summary>
    /// 启动步骤流程
    /// </summary>
    /// <param name="workflowRuntimeContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Result<Dictionary<string, object>>> ExecuteStepsAsync(
        WorkflowRuntimeContext workflowRuntimeContext, CancellationToken cancellationToken = default)
    {
        // 严格根据 InputMappings 从全局变量池填充步骤的内部变量池
        foreach ((string localName, string globalName) in this.Config.InputMappings)
        {
            if (!workflowRuntimeContext.GlobalVariables.TryGetValue(globalName, out object? value))
            {
                // 这一层校验理论上在静态分析时已完成，但在这里，“直接获取然后失败时抛错误”和“失败时返回Result”是一样的。
                return NormalError.Conflict($"执行步骤 '{this.Config.ConfigId}' 失败：找不到必需的全局输入变量 '{globalName}'。");
            }

            this.StepContent.StepVariable[localName] = value;
        }

        // this.StepContent.StepVariable[nameof(WorkflowRuntimeContext.FinalRawText)] = workflowRuntimeContext.FinalRawText;
        // this.StepContent.StepVariable[nameof(WorkflowRuntimeContext.GeneratedOperations)] = workflowRuntimeContext.GeneratedOperations;

        foreach (var module in this.Modules)
        {
            switch (module)
            {
                case INormalModule normalModule:
                    var result = await normalModule.ExecuteAsync(this.StepContent, cancellationToken);
                    if (result.TryGetError(out var error))
                        return error;
                    break;
            }
        }

        var stepOutput = new Dictionary<string, object>();

        // if (this.StepContent.StepVariable.TryGetValue(nameof(WorkflowRuntimeContext.FinalRawText), out object? finalRawText))
        // {
        //     workflowRuntimeContext.FinalRawText = (string)finalRawText;
        // }
        //
        // if (this.StepContent.StepVariable.TryGetValue(nameof(WorkflowRuntimeContext.GeneratedOperations), out object? generatedOperations))
        // {
        //     workflowRuntimeContext.GeneratedOperations = (List<AtomicOperation>)generatedOperations;
        // }

        foreach ((string globalName, string localName) in this.Config.OutputMappings)
        {
            // 从本步骤的内部变量池中查找由模块产生的局部变量
            if (this.StepContent.StepVariable.TryGetValue(localName, out object? localValue))
            {
                stepOutput[globalName] = localValue;
            }
            // else 
            // {
            //   可选：在这里可以处理映射声明了，但模块实际并未产生输出的情况
            //   例如：记录一个警告日志，或者根据严格模式抛出异常
            //   根据“后端不验证”的原则，我们暂时忽略这种情况
            // }
        }

        return stepOutput;
    }


    /// <inheritdoc />
    public IStepProcessorDebugDto DebugDto => new StepProcessorDebugDto
        { ModuleProcessorDebugDtos = this.Modules.ConvertAll(it => it.DebugDto) };

    /// <inheritdoc />
    internal record StepProcessorDebugDto : IStepProcessorDebugDto
    {
        /// <inheritdoc />
        public required IList<IModuleProcessorDebugDto> ModuleProcessorDebugDtos { get; init; }
    }
}