using FluentResults;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Module.ExactModule;
using static YAESandBox.Workflow.WorkflowProcessor;

namespace YAESandBox.Workflow.Step;

//step的信息：
// 使用的脚本模块们的UUID（注意，脚本模块本身就是绑定在步骤上的，如果需要把模块复制到更广的地方，可以考虑直接复制步骤之类的）
/// <summary>
/// 步骤配置的运行时
/// </summary>
internal class StepProcessor(
    WorkflowProcessorContent workflowContent,
    StepProcessorConfig config,
    List<IWithDebugDto<IModuleProcessorDebugDto>> modules,
    Dictionary<string, object> stepInput)
    : IWithDebugDto<IStepProcessorDebugDto>
{
    private StepProcessorContent StepContent { get; } = new(stepInput);

    private List<IWithDebugDto<IModuleProcessorDebugDto>> Modules { get; } = modules;
    private StepAiConfig? StepAiConfig { get; } = config.StepAiConfig;
    private WorkflowProcessorContent WorkflowContent { get; } = workflowContent;

    /// <summary>
    /// 启动步骤流程
    /// </summary>
    /// <returns></returns>
    public async Task<Result<Dictionary<string, object>>> ExecuteStepsAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, object> stepOutput = [];
        // TODO 之后把这里的switch改成直接调用方法，更为优雅。 AIModule由于和Step高度耦合，所以考虑特殊处理，其他的则实现统一的接口
        foreach (var module in this.Modules)
        {
            switch (module)
            {
                case AiModuleProcessor aiModule:
                    if (this.StepAiConfig?.SelectedAiModuleType == null || this.StepAiConfig.AiProcessorConfigUuid == null)
                        return NormalError.Conflict($"步骤 {this} 没有配置AI信息，所以无法执行AI模块。");
                    var aiProcessor = this.WorkflowContent.MasterAiService.CreateAiProcessor(
                        this.StepAiConfig.AiProcessorConfigUuid,
                        this.StepAiConfig.SelectedAiModuleType);
                    if (aiProcessor == null)
                        return NormalError.Conflict(
                            $"未找到 AI 配置 {this.StepAiConfig.AiProcessorConfigUuid}配置下的类型：{this.StepAiConfig.SelectedAiModuleType}");
                    var resultAi = await aiModule.ExecuteAsync(aiProcessor, this.StepContent.Prompts, this.StepAiConfig.IsStream,
                        cancellationToken);
                    if (!resultAi.TryGetValue(out string? value))
                        return resultAi.ToResult();
                    this.StepContent.FullAiReturn = value;
                    break;
                case PromptGenerationModuleProcessor promptGenerationModule:
                    var resultPromptGeneration = await promptGenerationModule.ExecuteAsync(this.StepContent);
                    break;
                case TemporaryAiOutputToRawTextModuleProcessor temporaryAiOutputToRawTextModule:
                    var resultTemporaryAiOutputToRawText =
                        await temporaryAiOutputToRawTextModule.ExecuteAsync(this.WorkflowContent, this.StepContent);
                    break;
            }
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

    /// <summary>
    /// 步骤运行时的上下文
    /// </summary>
    /// <param name="stepInput"></param>
    public class StepProcessorContent(Dictionary<string, object> stepInput)
    {
        // TODO 之后应该根据需求进行拷贝
        public dynamic StepInput { get; } = stepInput.ToDictionary(kv => kv.Key, kv => kv.Value);
        public List<RoledPromptDto> Prompts { get; } = [];
        public string? FullAiReturn { get; set; }
    }
}