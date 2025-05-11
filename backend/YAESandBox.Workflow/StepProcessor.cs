using FluentResults;
using YAESandBox.Workflow.AIService;

namespace YAESandBox.Workflow;

//step的信息：
// 使用的脚本模块们的UUID（注意，脚本模块本身就是绑定在步骤上的，如果需要把模块复制到更广的地方，可以考虑直接复制步骤之类的）
public class StepProcessor
{
    internal StepProcessor(WorkflowProcessor.WorkflowProcessorContent workflowProcessor,
        StepProcessorConfig config,
        Dictionary<string, object> stepInput)
    {
        this.workflowProcessor = workflowProcessor;
        this.content = new StepProcessorContent(stepInput);
        this.modules = config.moduleIds.ConvertAll(it => ConfigLocator.findModuleConfig(it).ToModule(this.content));
        this.stepAiConfig = config.stepAiConfig;
    }

    public class StepProcessorContent(Dictionary<string, object> stepInput)
    {
        // TODO 之后应该根据需求进行拷贝
        public dynamic stepInput { get; } = stepInput.ToDictionary(kv => kv.Key, kv => kv.Value);
        public List<(PromptRole role, string prompt)> prompts { get; } = [];
        public string? fullAiReturn { get; set; }
    }

    private StepProcessorContent content { get; }

    private IList<IWorkflowModule> modules { get; }
    private StepAiConfig stepAiConfig { get; }
    private WorkflowProcessor.WorkflowProcessorContent workflowProcessor { get; }

    public async Task<Result<Dictionary<string, object>>> ExecuteStepsAsync()
    {
        Dictionary<string, object> stepOutput = [];
        foreach (var module in this.modules)
        {
            switch (module)
            {
                case AiModule aiModule:
                    if (this.stepAiConfig.SelectedAiModuleType == null)
                        return AiError.Error($"请先配置 {this.stepAiConfig.AiProcessorConfigUUID} 的AI类型。");
                    var aiProcessor = this.workflowProcessor.MasterAiService.CreateAiProcessor(
                        this.stepAiConfig.AiProcessorConfigUUID,
                        this.stepAiConfig.SelectedAiModuleType);
                    if (aiProcessor == null)
                        return AiError
                            .Error($"未找到 AI 配置 {this.stepAiConfig.AiProcessorConfigUUID}配置下的类型：{this.stepAiConfig.SelectedAiModuleType}");
                    var result = await aiModule.ExecuteAsync(aiProcessor, this.content.prompts, this.stepAiConfig.IsStream);
                    if (result.IsFailed)
                        return result.ToResult();
                    this.content.fullAiReturn = result.Value;
                    break;
            }
        }

        return stepOutput;
    }
}

public record StepProcessorConfig(StepAiConfig stepAiConfig, List<string> moduleIds)
{
    public StepProcessor ToStepProcessor(WorkflowProcessor.WorkflowProcessorContent workflowProcessor, Dictionary<string, object> stepInput)
    {
        return new StepProcessor(workflowProcessor, this, stepInput);
    }
}

/// <summary>
/// 步骤本身的 AI 配置。
/// </summary>
/// <param name="AiProcessorConfigUUID">AI服务的配置的UUID</param>
/// <param name="SelectedAiModuleType">当前选中的AI模型的类型名，需要通过<see cref="IMasterAiService.GetAbleAiProcessorType"/>获取</param>
/// <param name="IsStream">是否为流式传输</param>
public record StepAiConfig(string AiProcessorConfigUUID, string? SelectedAiModuleType, bool IsStream);