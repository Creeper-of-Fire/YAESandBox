using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.Core.Action;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.AIService;

namespace YAESandBox.Workflow;

public class WorkflowProcessor
{
    private Dictionary<string, object> Variables { get; }
    private List<StepProcessor> Steps { get; }

    private WorkflowProcessorContent Content { get; init; }

    public class WorkflowProcessorContent(
        IMasterAiService masterAiService,
        IWorkflowDataAccess dataAccess,
        Action<DisplayUpdateRequestPayload> requestDisplayUpdateCallback)
    {
        public IMasterAiService MasterAiService { get; } = masterAiService;

        public IWorkflowDataAccess DataAccess { get; } = dataAccess;

        public void RequestDisplayUpdateCallback(string content, UpdateMode updateMode = UpdateMode.FullSnapshot) =>
            requestDisplayUpdateCallback(new DisplayUpdateRequestPayload(content, updateMode));

        public string? RawText { get; set; }
        public List<AtomicOperation> Operations { get; } = [];
    }


    public WorkflowProcessor(string workflowId,
        IReadOnlyDictionary<string, string> triggerParams,
        IMasterAiService masterAiService,
        IWorkflowDataAccess dataAccess,
        Action<DisplayUpdateRequestPayload> requestDisplayUpdateCallback,
        CancellationToken cancellationToken = default)
    {
        this.Content = new WorkflowProcessorContent(masterAiService, dataAccess, requestDisplayUpdateCallback);
        var workflowProcessorConfig = ConfigLocator.FindWorkflowProcessorConfig(workflowId);
        this.Variables = triggerParams.ToDictionary(kv => kv.Key, object (kv) => kv.Value);
        this.Steps = workflowProcessorConfig.StepIds.ConvertAll(it =>
            ConfigLocator.FindStepProcessorConfig(it).ToStepProcessor(this.Content, this.Variables));
    }

    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync()
    {
        foreach (var step in this.Steps)
        {
            // TODO 变量池输出
            await step.ExecuteStepsAsync();
        }

        return new WorkflowExecutionResult(true, null, null, this.Content.Operations, this.Content.RawText ?? "");
    }
}

public record WorkflowProcessorConfig(List<string> StepIds);