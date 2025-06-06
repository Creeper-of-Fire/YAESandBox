using YAESandBox.Core.Action;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Step;
using YAESandBox.Workflow.Utility;
using static YAESandBox.Workflow.WorkflowProcessor;

namespace YAESandBox.Workflow;

internal class WorkflowProcessor(
    WorkflowRuntimeService runtimeService,
    WorkflowProcessorConfig config,
    Dictionary<string, string> triggerParams)
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

    private List<StepProcessor> Steps { get; } = config.Steps.ConvertAll(it => it.ToStepProcessor(runtimeService));

    private WorkflowRuntimeService RuntimeService { get; } = runtimeService;
    private WorkflowRuntimeContext Context { get; } = new(triggerParams);

    /// <summary>
    /// 封装工作流执行所需的无状态、只读的外部服务。
    /// 这个对象在工作流启动时创建一次，并被传递给所有需要它的组件。
    /// </summary>
    /// <param name="masterAiService"></param>
    /// <param name="dataAccess"></param>
    /// <param name="requestDisplayUpdateCallback"></param>
    public class WorkflowRuntimeService(
        IMasterAiService masterAiService,
        IWorkflowDataAccess dataAccess,
        Action<DisplayUpdateRequestPayload> requestDisplayUpdateCallback)
    {
        public IMasterAiService MasterAiService { get; } = masterAiService;

        public IWorkflowDataAccess DataAccess { get; } = dataAccess;

        public void RequestDisplayUpdateCallback(string content, UpdateMode updateMode = UpdateMode.FullSnapshot) =>
            requestDisplayUpdateCallback(new DisplayUpdateRequestPayload(content, updateMode));
    }

    /// <summary>
    /// 封装工作流执行期间的有状态数据。
    /// 这个对象是可变的，并在整个工作流的步骤之间传递和修改。
    /// </summary>
    public class WorkflowRuntimeContext(IReadOnlyDictionary<string, string> triggerParams)
    {
        /// <summary>
        /// 工作流的输入参数被放入这里，
        /// </summary>
        public IReadOnlyDictionary<string,string> TriggerParams { get; } = triggerParams;

        /// <summary>
        /// 全局变量池。
        /// 每个步骤的输出可以写回这里，供后续步骤使用。
        /// </summary>
        public Dictionary<string, object> GlobalVariables { get; } = [];

        /// <summary>
        /// 最终生成的、要呈现给用户的原始文本。
        /// </summary>
        public string FinalRawText { get; set; } = string.Empty;

        /// <summary>
        /// 整个工作流生成的所有原子操作列表。
        /// </summary>
        public List<AtomicOperation> GeneratedOperations { get; set; } = [];
    }


    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(CancellationToken cancellationToken = default)
    {
        // 依次执行工作流中的每一个步骤
        foreach (var step in this.Steps)
        {
            // 调用步骤的执行方法，并传入全局上下文
            var stepResult = await step.ExecuteStepsAsync(this.Context, cancellationToken);

            // 检查步骤执行是否成功
            if (stepResult.IsFailed)
            {
                // 如果步骤执行失败，立即中断整个工作流，并构造一个失败的执行结果
                return new WorkflowExecutionResult(
                    IsSuccess: false,
                    ErrorMessage: stepResult.Errors.FirstOrDefault()?.Message ?? "步骤执行时发生未知错误。",
                    ErrorCode: "StepExecutionFailed", // 可以定义一个更具体的错误码
                    Operations: this.Context.GeneratedOperations, // 返回到目前为止已生成的操作
                    RawText: this.Context.FinalRawText ?? "" // 返回到目前为止已生成的文本
                );
            }

            // 如果步骤执行成功，将其输出的变量合并到全局变量池中
            if (!stepResult.TryGetValue(out var stepOutput)) continue;

            foreach (var outputVariable in stepOutput)
            {
                // 将步骤的输出写入或覆盖到全局上下文中
                this.Context.GlobalVariables[outputVariable.Key] = outputVariable.Value;
            }
        }

        // 所有步骤都成功执行完毕后，构造一个成功的最终结果
        return new WorkflowExecutionResult(
            IsSuccess: true,
            ErrorMessage: null,
            ErrorCode: null,
            Operations: this.Context.GeneratedOperations,
            RawText: this.Context.FinalRawText ?? ""
        );
    }
}