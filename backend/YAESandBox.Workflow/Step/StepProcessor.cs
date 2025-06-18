using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Module;
using YAESandBox.Workflow.Module.ExactModule;
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
    private StepProcessorContent StepContent { get; } = new();

    /// <summary>
    /// 消费者（Consumes）：模块需要的所有输入变量，这些必须从全局变量池中获取。
    /// </summary>
    internal IEnumerable<string> GlobalConsumers { get; } = config.Modules.SelectMany(c => c.Consumes).Distinct();

    /// <summary>
    /// 生产者（Produces）：此步骤通过 OutputMappings 向全局变量池声明输出的变量。
    /// </summary>
    internal IEnumerable<string> GlobalProducers { get; } = config.OutputMappings.Keys;

    private List<IWithDebugDto<IModuleProcessorDebugDto>> Modules { get; } =
        config.Modules.Select(module => module.ToModuleProcessor(workflowRuntimeService)).ToList();

    private StepAiConfig? StepAiConfig { get; } = config.StepAiConfig;
    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

    /// <summary>
    /// 启动步骤流程
    /// </summary>
    /// <param name="workflowRuntimeContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Result<Dictionary<string, object>>> ExecuteStepsAsync(
        WorkflowRuntimeContext workflowRuntimeContext, CancellationToken cancellationToken = default)
    {
        foreach (string consumerName in this.GlobalConsumers)
        {
            if (!workflowRuntimeContext.GlobalVariables.TryGetValue(consumerName, out object? value))
            {
                return NormalError.Conflict($"执行步骤 '{this.Config.ConfigId}' 失败：找不到必需的输入变量 '{consumerName}'。");
            }

            this.StepContent.StepVariable[consumerName] = value;
        }

        foreach ((string key, string value) in workflowRuntimeContext.TriggerParams)
        {
            this.StepContent.StepVariable[key] = value;
        }

        // this.StepContent.StepVariable[nameof(WorkflowRuntimeContext.FinalRawText)] = workflowRuntimeContext.FinalRawText;
        // this.StepContent.StepVariable[nameof(WorkflowRuntimeContext.GeneratedOperations)] = workflowRuntimeContext.GeneratedOperations;

        foreach (var module in this.Modules)
        {
            switch (module)
            {
                case AiModuleProcessor aiModule:
                    var resultAi = await this.PrepareAndExecuteAiModule(aiModule, cancellationToken);
                    if (resultAi.TryGetError(out var error1, out string? value))
                        return error1;
                    this.StepContent.FullAiReturn = value;
                    break;

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

    private async Task<Result<string>> PrepareAndExecuteAiModule(AiModuleProcessor aiModule, CancellationToken cancellationToken = default)
    {
        if (this.StepAiConfig?.SelectedAiModuleType == null || this.StepAiConfig.AiProcessorConfigUuid == null)
            return NormalError.Conflict($"步骤 {this} 没有配置AI信息，所以无法执行AI模块。");
        var aiProcessor = this.WorkflowRuntimeService.MasterAiService.CreateAiProcessor(
            this.StepAiConfig.AiProcessorConfigUuid,
            this.StepAiConfig.SelectedAiModuleType);
        if (aiProcessor == null)
            return NormalError.Conflict(
                $"未找到 AI 配置 {this.StepAiConfig.AiProcessorConfigUuid}配置下的类型：{this.StepAiConfig.SelectedAiModuleType}");
        return await aiModule.ExecuteAsync(aiProcessor, this.StepContent.Prompts, this.StepAiConfig.IsStream, cancellationToken);
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

    /// <summary>
    /// 步骤运行时的上下文
    /// </summary>
    public class StepProcessorContent
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

        public List<RoledPromptDto> Prompts { get; } = [];
        public string? FullAiReturn { get; set; }
    }
}