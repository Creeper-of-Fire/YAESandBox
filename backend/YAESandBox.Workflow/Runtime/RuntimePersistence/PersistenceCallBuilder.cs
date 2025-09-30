using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.Runtime.RuntimePersistence;

/// <summary>
/// 一个流畅的构造器，用于构建和执行一个持久化操作。
/// </summary>
/// <typeparam name="TInput">操作的输入类型。</typeparam>
public class PersistenceCallBuilder<TInput>
{
    private WorkflowPersistenceService PersistenceService { get; }
    private Guid InstanceId { get; }
    private TInput Inputs { get; }

    internal PersistenceCallBuilder(WorkflowPersistenceService persistenceService, Guid instanceId, TInput inputs)
    {
        this.PersistenceService = persistenceService;
        this.InstanceId = instanceId;
        this.Inputs = inputs;
    }

    /// <summary>
    /// 指定一个操作逻辑并执行。
    /// </summary>
    /// <param name="executionLogic">核心业务逻辑。</param>
    public Task<Result<TOutput?>> ExecuteAsync<TOutput>(Func<TInput, Task<Result<TOutput>>> executionLogic)
    {
        // 委托给 WorkflowPersistenceService 的内部实现
        return this.PersistenceService.ExecuteInternalAsync(this.InstanceId, this.Inputs, executionLogic);
    }

    /// <summary>
    /// 指定一个【没有输出】的操作逻辑并执行。
    /// </summary>
    /// <param name="executionLogic">核心业务逻辑。</param>
    public Task<Result> ExecuteAsync(Func<TInput, Task<Result>> executionLogic)
    {
        // 委托给 WorkflowPersistenceService 的内部实现
        return this.PersistenceService.ExecuteInternalAsync(this.InstanceId, this.Inputs, executionLogic);
    }

    /// <summary>
    /// 指定一个【输出不可为 null】的操作逻辑并执行。
    /// </summary>
    /// <typeparam name="TOutput">输出类型，约束为 notnull。</typeparam>
    /// <param name="executionLogic">核心业务逻辑。</param>
    public Task<Result<TOutput>> ExecuteNonNullAsync<TOutput>(Func<TInput, Task<Result<TOutput>>> executionLogic)
        where TOutput : notnull
    {
        // 委托给 WorkflowPersistenceService 的内部实现
        return this.PersistenceService.ExecuteNonNullInternalAsync(this.InstanceId, this.Inputs, executionLogic);
    }
}