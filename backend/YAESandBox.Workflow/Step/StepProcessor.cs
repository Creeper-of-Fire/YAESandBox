using FluentResults;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Module;
using static YAESandBox.Workflow.WorkflowProcessor;

namespace YAESandBox.Workflow.Step;

//step的信息：
// 使用的脚本模块们的UUID（注意，脚本模块本身就是绑定在步骤上的，如果需要把模块复制到更广的地方，可以考虑直接复制步骤之类的）
/// <summary>
/// 步骤配置的运行时
/// </summary>
public class StepProcessor : IWithDebugDto<IStepProcessorDebugDto>
{
    /// <inheritdoc />
    public IStepProcessorDebugDto DebugDto => new StepProcessorDebugDto
        { ModuleProcessorDebugDtos = this.Modules.ConvertAll(it => it.DebugDto) };

    /// <inheritdoc />
    internal record StepProcessorDebugDto : IStepProcessorDebugDto
    {
        /// <inheritdoc />
        public required IList<IModuleProcessorDebugDto> ModuleProcessorDebugDtos { get; init; }
    }

    internal StepProcessor(WorkflowProcessorContent workflowProcessor,
        StepProcessorConfig config,
        Dictionary<string, object> stepInput)
    {
        this.WorkflowProcessor = workflowProcessor;
        this.Content = new StepProcessorContent(stepInput);
        this.Modules =
            config.ModuleIds.ConvertAll(id =>
                (config.InnerModuleConfig.TryGetValue(id, out var value) ? value : ConfigLocator.FindModuleConfig(id))
                .ToModule(this.Content));
        this.StepAiConfig = config.StepAiConfig;
    }

    /// <summary>
    /// 步骤运行时的上下文
    /// </summary>
    /// <param name="stepInput"></param>
    public class StepProcessorContent(Dictionary<string, object> stepInput)
    {
        // TODO 之后应该根据需求进行拷贝
        public dynamic StepInput { get; } = stepInput.ToDictionary(kv => kv.Key, kv => kv.Value);
        public IEnumerable<RoledPromptDto> Prompts { get; } = [];
        public string? FullAiReturn { get; set; }
    }

    private StepProcessorContent Content { get; }

    private List<IModuleProcessor> Modules { get; }
    private StepAiConfig? StepAiConfig { get; }
    private WorkflowProcessorContent WorkflowProcessor { get; }

    /// <summary>
    /// 启动步骤流程
    /// </summary>
    /// <returns></returns>
    public async Task<Result<Dictionary<string, object>>> ExecuteStepsAsync()
    {
        Dictionary<string, object> stepOutput = [];
        foreach (var module in this.Modules)
        {
            switch (module)
            {
                case AiModuleProcessor aiModule:
                    if (this.StepAiConfig?.SelectedAiModuleType == null || this.StepAiConfig.AiProcessorConfigUuid == null)
                        return NormalError.Conflict($"步骤 {this} 没有配置AI信息，所以无法执行AI模块。");
                    var aiProcessor = this.WorkflowProcessor.MasterAiService.CreateAiProcessor(
                        this.StepAiConfig.AiProcessorConfigUuid,
                        this.StepAiConfig.SelectedAiModuleType);
                    if (aiProcessor == null)
                        return NormalError.Conflict(
                            $"未找到 AI 配置 {this.StepAiConfig.AiProcessorConfigUuid}配置下的类型：{this.StepAiConfig.SelectedAiModuleType}");
                    var result = await aiModule.ExecuteAsync(aiProcessor, this.Content.Prompts, this.StepAiConfig.IsStream);
                    if (!result.TryGetValue(out string? value))
                        return result.ToResult();
                    this.Content.FullAiReturn = value;
                    break;
            }
        }

        return stepOutput;
    }
}