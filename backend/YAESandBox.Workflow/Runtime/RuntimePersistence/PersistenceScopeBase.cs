namespace YAESandBox.Workflow.Runtime.RuntimePersistence;

/// <summary>
/// 持久化执行作用域的抽象基类。
/// 封装了输入、状态和生命周期管理的核心逻辑。
/// </summary>
public abstract class PersistenceScopeBase<TInput> : IAsyncDisposable
{
    /// <summary>
    /// 持久化服务
    /// </summary>
    protected WorkflowPersistenceService PersistenceService { get; }

    /// <summary>
    /// 实例的唯一性ID
    /// </summary>
    protected Guid InstanceId { get; }

    /// <summary>
    /// 输入的序列化结果
    /// </summary>
    protected string SerializedInputs { get; }

    /// <summary>
    /// 指示是否需要执行核心业务逻辑。
    /// </summary>
    public bool ShouldExecute { get; }

    /// <summary>
    /// 【仅当 ShouldExecute 为 true 时可用】
    /// 应该用于执行的有效输入。
    /// </summary>
    public TInput EffectiveInputs { get; }

    internal PersistenceScopeBase(
        WorkflowPersistenceService persistenceService,
        Guid instanceId,
        bool shouldExecute,
        TInput effectiveInputs,
        string serializedInputs)
    {
        this.PersistenceService = persistenceService;
        this.InstanceId = instanceId;
        this.ShouldExecute = shouldExecute;
        this.EffectiveInputs = effectiveInputs;
        this.SerializedInputs = serializedInputs;
    }

    /// <summary>
    /// 当 `await using` 块结束时，此方法被自动调用。
    /// 子类必须实现此方法以保存最终状态。
    /// </summary>
    public abstract ValueTask DisposeAsync();
}