using FluentResults;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Module;

namespace YAESandBox.Workflow;

//step的信息：
// 使用的脚本模块们的UUID（注意，脚本模块本身就是绑定在步骤上的，如果需要把模块复制到更广的地方，可以考虑直接复制步骤之类的）
internal class StepProcessor
{
    public StepProcessor(WorkflowProcessor.WorkflowProcessorContent workflowProcessor,
        StepProcessorConfig config,
        Dictionary<string, object> stepInput)
    {
        this.WorkflowProcessor = workflowProcessor;
        this.Content = new StepProcessorContent(stepInput);
        this.Modules = config.ModuleIds.ConvertAll(it => ConfigLocator.FindModuleConfig(it).ToModule(this.Content));
        this.StepAiConfig = config.StepAiConfig;
    }

    public class StepProcessorContent(Dictionary<string, object> stepInput)
    {
        // TODO 之后应该根据需求进行拷贝
        public dynamic StepInput { get; } = stepInput.ToDictionary(kv => kv.Key, kv => kv.Value);
        public IEnumerable<RoledPromptDto> Prompts { get; } = [];
        public string? FullAiReturn { get; set; }
    }

    private StepProcessorContent Content { get; }

    private IList<IWorkflowModule> Modules { get; }
    private StepAiConfig StepAiConfig { get; }
    private WorkflowProcessor.WorkflowProcessorContent WorkflowProcessor { get; }

    public async Task<Result<Dictionary<string, object>>> ExecuteStepsAsync()
    {
        Dictionary<string, object> stepOutput = [];
        foreach (var module in this.Modules)
        {
            switch (module)
            {
                case AiModule aiModule:
                    if (this.StepAiConfig.SelectedAiModuleType == null)
                        return AiError.Error($"请先配置 {this.StepAiConfig.AiProcessorConfigUuid} 的AI类型。");
                    var aiProcessor = this.WorkflowProcessor.MasterAiService.CreateAiProcessor(
                        this.StepAiConfig.AiProcessorConfigUuid,
                        this.StepAiConfig.SelectedAiModuleType);
                    if (aiProcessor == null)
                        return AiError
                            .Error($"未找到 AI 配置 {this.StepAiConfig.AiProcessorConfigUuid}配置下的类型：{this.StepAiConfig.SelectedAiModuleType}");
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

public record StepProcessorConfig(StepAiConfig StepAiConfig, List<string> ModuleIds)
{
    internal StepProcessor ToStepProcessor(WorkflowProcessor.WorkflowProcessorContent workflowProcessor,
        Dictionary<string, object> stepInput)
    {
        return new StepProcessor(workflowProcessor, this, stepInput);
    }
}

/// <summary>
/// 步骤本身的 AI 配置。
/// </summary>
/// <param name="AiProcessorConfigUuid">AI服务的配置的UUID</param>
/// <param name="SelectedAiModuleType">当前选中的AI模型的类型名，需要通过<see cref="IMasterAiService.GetAbleAiProcessorType"/>获取</param>
/// <param name="IsStream">是否为流式传输</param>
public record StepAiConfig(string AiProcessorConfigUuid, string? SelectedAiModuleType, bool IsStream);