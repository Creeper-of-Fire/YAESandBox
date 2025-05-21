using Nito.AsyncEx;
using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.Core.Action;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Step;

namespace YAESandBox.Workflow;

public class WorkflowProcessor : IWithDebugDto<IWorkflowProcessorDebugDto>
{
    private WorkflowProcessor(
        WorkflowProcessorContent content,
        List<StepProcessor> steps,
        Dictionary<string, object> variables)
    {
        this.Variables = variables;
        this.Steps = steps;
        this.Content = content;
    }

    public static async Task<WorkflowProcessor> CreateAsync(
        WorkflowConfigService workflowConfigService,
        string workflowId,
        IReadOnlyDictionary<string, string> triggerParams,
        IMasterAiService masterAiService,
        IWorkflowDataAccess dataAccess,
        Action<DisplayUpdateRequestPayload> requestDisplayUpdateCallback)
    {
        var content = new WorkflowProcessorContent(masterAiService, dataAccess, requestDisplayUpdateCallback);
        var workflowProcessorConfig = await ConfigLocator.FindWorkflowProcessorConfig(workflowConfigService, workflowId);
        var variables = triggerParams.ToDictionary(kv => kv.Key, object (kv) => kv.Value);

        var steps2 = await workflowProcessorConfig.StepIds.ConvertAll(Converter).WhenAll();
        var steps = steps2.ToList();
        return new WorkflowProcessor(content, steps, variables);

        async Task<StepProcessor> Converter(string it)
        {
            var step0 = await ConfigLocator.FindStepProcessorConfig(workflowConfigService, it);
            var step1 = await step0.ToStepProcessorAsync(workflowConfigService, content, variables);
            return step1;
        }
    }

    /// <inheritdoc />
    public IWorkflowProcessorDebugDto DebugDto => new WorkflowProcessorDebugDto
    {
        StepProcessorDebugDtos = this.Steps.ConvertAll(it => it.DebugDto)
    };

    /// <inheritdoc />
    public record WorkflowProcessorDebugDto : IWorkflowProcessorDebugDto
    {
        /// <inheritdoc />
        public required IList<IStepProcessorDebugDto> StepProcessorDebugDtos { get; init; }
    }

    private Dictionary<string, object> Variables { get; }
    private List<StepProcessor> Steps { get; }

    private WorkflowProcessorContent Content { get; }

    /// <summary>
    /// 工作流的执行上下文
    /// </summary>
    /// <param name="masterAiService"></param>
    /// <param name="dataAccess"></param>
    /// <param name="requestDisplayUpdateCallback"></param>
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


    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(CancellationToken cancellationToken = default)
    {
        foreach (var step in this.Steps)
        {
            // TODO 变量池输出
            await step.ExecuteStepsAsync( cancellationToken);
        }

        return new WorkflowExecutionResult(true, null, null, this.Content.Operations, this.Content.RawText ?? "");
    }
}

public record WorkflowProcessorConfig(List<string> StepIds);