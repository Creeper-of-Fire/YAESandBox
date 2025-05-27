using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.Core.Action;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Step;
using static YAESandBox.Workflow.WorkflowProcessor;

namespace YAESandBox.Workflow;

internal class WorkflowProcessor(
    WorkflowProcessorContent content,
    List<StepProcessor> steps,
    Dictionary<string, object> variables)
    : IWithDebugDto<IWorkflowProcessorDebugDto>
{
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

    private Dictionary<string, object> Variables { get; } = variables;
    private List<StepProcessor> Steps { get; } = steps;

    private WorkflowProcessorContent Content { get; } = content;

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
            await step.ExecuteStepsAsync(cancellationToken);
        }

        return new WorkflowExecutionResult(true, null, null, this.Content.Operations, this.Content.RawText ?? "");
    }
}