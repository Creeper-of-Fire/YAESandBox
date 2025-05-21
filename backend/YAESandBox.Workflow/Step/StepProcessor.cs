using FluentResults;
using Nito.AsyncEx;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService;
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
public class StepProcessor : IWithDebugDto<IStepProcessorDebugDto>
{
    private StepProcessor(
        WorkflowProcessorContent workflowProcessorContent,
        StepProcessorConfig config,
        List<IModuleProcessor> modules,
        Dictionary<string, object> stepInput)
    {
        this.WorkflowProcessorContent = workflowProcessorContent;
        this.Content = new StepProcessorContent(stepInput);
        this.Modules = modules;
        this.StepAiConfig = config.StepAiConfig;
    }

    internal static async Task<StepProcessor> CreateAsync(
        WorkflowConfigService workflowConfigService,
        WorkflowProcessorContent workflowProcessorContent,
        StepProcessorConfig config,
        Dictionary<string, object> stepInput)
    {
        var stepProcessorContent = new StepProcessorContent(stepInput);
        var modules =
            (await config.ModuleIds.ConvertAll(async id =>
            {
                if (config.InnerModuleConfig.TryGetValue(id, out var value))
                {
                    return await value.ToModuleAsync(workflowConfigService, stepProcessorContent);
                }

                return await (await ConfigLocator.FindModuleConfig(workflowConfigService, id))
                    .ToModuleAsync(workflowConfigService, stepProcessorContent);
            }).WhenAll()).ToList();
        return new StepProcessor(workflowProcessorContent, config, modules, stepInput);
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

    private StepProcessorContent Content { get; }

    private List<IModuleProcessor> Modules { get; }
    private StepAiConfig? StepAiConfig { get; }
    private WorkflowProcessorContent WorkflowProcessorContent { get; }

    /// <summary>
    /// 启动步骤流程
    /// </summary>
    /// <returns></returns>
    public async Task<Result<Dictionary<string, object>>> ExecuteStepsAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, object> stepOutput = [];
        foreach (var module in this.Modules)
        {
            switch (module)
            {
                case AiModuleProcessor aiModule:
                    if (this.StepAiConfig?.SelectedAiModuleType == null || this.StepAiConfig.AiProcessorConfigUuid == null)
                        return NormalError.Conflict($"步骤 {this} 没有配置AI信息，所以无法执行AI模块。");
                    var aiProcessor = this.WorkflowProcessorContent.MasterAiService.CreateAiProcessor(
                        this.StepAiConfig.AiProcessorConfigUuid,
                        this.StepAiConfig.SelectedAiModuleType);
                    if (aiProcessor == null)
                        return NormalError.Conflict(
                            $"未找到 AI 配置 {this.StepAiConfig.AiProcessorConfigUuid}配置下的类型：{this.StepAiConfig.SelectedAiModuleType}");
                    var result = await aiModule.ExecuteAsync(aiProcessor, this.Content.Prompts, this.StepAiConfig.IsStream, cancellationToken);
                    if (!result.TryGetValue(out string? value))
                        return result.ToResult();
                    this.Content.FullAiReturn = value;
                    break;
            }
        }

        return stepOutput;
    }
}