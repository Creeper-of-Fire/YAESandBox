using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.Core.Action;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.AIService;

namespace YAESandBox.Workflow;

public class WorkflowProcessor
{
    private Dictionary<string, object> variables { get; }
    private List<StepProcessor> steps { get; }

    private WorkflowProcessorContent content { get; init; }

    public class WorkflowProcessorContent(
        IMasterAiService masterAiService,
        IWorkflowDataAccess dataAccess,
        Action<DisplayUpdateRequestPayload> requestDisplayUpdateCallback)
    {
        public IMasterAiService MasterAiService { get; } = masterAiService;

        public IWorkflowDataAccess DataAccess { get; } = dataAccess;

        public void RequestDisplayUpdateCallback(string Content, UpdateMode UpdateMode = UpdateMode.FullSnapshot) =>
            requestDisplayUpdateCallback(new DisplayUpdateRequestPayload(Content, UpdateMode));

        public string? rawText { get; set; }
        public List<AtomicOperation> operations { get; } = [];
    }


    public WorkflowProcessor(string workflowId,
        IReadOnlyDictionary<string, string> triggerParams,
        IMasterAiService masterAiService,
        IWorkflowDataAccess dataAccess,
        Action<DisplayUpdateRequestPayload> requestDisplayUpdateCallback,
        CancellationToken cancellationToken = default)
    {
        this.content = new WorkflowProcessorContent(masterAiService, dataAccess, requestDisplayUpdateCallback);
        var workflowProcessorConfig = ConfigLocator.findWorkflowProcessorConfig(workflowId);
        this.variables = triggerParams.ToDictionary(kv => kv.Key, object (kv) => kv.Value);
        this.steps = workflowProcessorConfig.stepIds.ConvertAll(it =>
            ConfigLocator.findStepProcessorConfig(it).ToStepProcessor(this.content, this.variables));
    }

    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync()
    {
        foreach (var step in this.steps)
        {
            // TODO 变量池输出
            await step.ExecuteStepsAsync();
        }

        return new WorkflowExecutionResult(true, null, null, this.content.operations, this.content.rawText ?? "");
    }
}

public record WorkflowProcessorConfig(List<string> stepIds);