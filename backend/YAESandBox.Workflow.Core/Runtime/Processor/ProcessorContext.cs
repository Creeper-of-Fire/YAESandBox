using YAESandBox.Workflow.Core.Runtime.WorkflowService;

namespace YAESandBox.Workflow.Core.Runtime.Processor;

/// <summary>
/// 为单个处理器实例提供唯一的运行时上下文。
/// 它包含了处理器的身份信息以及访问全局运行时服务的能力。
/// </summary>
public record ProcessorContext
{
    /// <summary>
    /// 当前处理器实例的唯一ID。
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// 创建此实例的父处理器实例的ID (可空，顶级处理器没有父级)。
    /// </summary>
    public Guid? ParentInstanceId { get; }

    /// <summary>
    /// 整个工作流执行的根实例ID。
    /// </summary>
    public Guid WorkflowInstanceId { get; }

    /// <summary>
    /// 对整个工作流共享的运行时服务的引用。
    /// </summary>
    public WorkflowRuntimeService RuntimeService { get; }

    // 程序集内部构造函数，防止外部直接创建
    internal ProcessorContext(Guid instanceId, Guid? parentInstanceId, Guid workflowInstanceId, WorkflowRuntimeService runtimeService)
    {
        this.InstanceId = instanceId;
        this.ParentInstanceId = parentInstanceId;
        this.WorkflowInstanceId = workflowInstanceId;
        this.RuntimeService = runtimeService;
    }

    /// <summary>
    /// 创建一个顶级（工作流级别）的处理器上下文。
    /// </summary>
    public static ICreatingContext CreateRoot(Guid workflowInstanceId, WorkflowRuntimeService runtimeService)
    {
        return new RootCreatingContext(new ProcessorContext(workflowInstanceId, null, workflowInstanceId, runtimeService));
    }

    /// <summary>
    /// 为即将创建的子处理器准备一个上下文创建信封。
    /// </summary>
    /// <param name="childInstanceId">由父级为子级分配的唯一ID，其必须在全局保持唯一。</param>
    public ICreatingContext CreateContextForChild(Guid childInstanceId)
    {
        return new CreatingContext(this, childInstanceId);
    }
}

/// <summary>
/// 一个临时的、用于安全地创建子处理器上下文的“信封”。
/// 它由父级创建，并携带了为子级准备好的所有信息。
/// 它的存在是为了强制执行正确的父子上下文创建流程。
/// </summary>
public interface ICreatingContext
{
    /// <summary>
    /// 从这个“信封”中解压出最终的、属于子处理器自己的 ProcessorContext。
    /// 这是子处理器获取其上下文的唯一途径。
    /// </summary>
    ProcessorContext ExtractContext();
}

/// <inheritdoc />
/// <remarks>它被用于创建根对象</remarks>
file sealed record RootCreatingContext(ProcessorContext ParentContext) : ICreatingContext
{
    private ProcessorContext ParentContext { get; } = ParentContext;

    /// <inheritdoc />
    public ProcessorContext ExtractContext()
    {
        return new ProcessorContext(
            instanceId: this.ParentContext.InstanceId,
            parentInstanceId: null,
            workflowInstanceId: this.ParentContext.WorkflowInstanceId,
            runtimeService: this.ParentContext.RuntimeService
        );
    }
}

/// <inheritdoc />
file sealed record CreatingContext(ProcessorContext ParentContext, Guid ChildInstanceId) : ICreatingContext
{
    private ProcessorContext ParentContext { get; } = ParentContext;
    private Guid ChildInstanceId { get; } = ChildInstanceId;

    /// <inheritdoc />
    public ProcessorContext ExtractContext()
    {
        return new ProcessorContext(
            instanceId: this.ChildInstanceId,
            parentInstanceId: this.ParentContext.InstanceId,
            workflowInstanceId: this.ParentContext.WorkflowInstanceId,
            runtimeService: this.ParentContext.RuntimeService
        );
    }
}