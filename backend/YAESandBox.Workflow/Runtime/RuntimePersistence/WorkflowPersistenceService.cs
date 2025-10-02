using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Workflow.Runtime.RuntimePersistence.Storage;

namespace YAESandBox.Workflow.Runtime.RuntimePersistence;

/// <summary>
/// 工作流持久化服务。
/// </summary>
public class WorkflowPersistenceService(IPersistenceStorage? persistenceStorage = null)
{
    /// <summary>
    /// 底层的存储实现。
    /// </summary>
    private IPersistenceStorage Storage { get; } = persistenceStorage ?? new InMemoryPersistenceStorage();

    /// <summary>
    /// 获取用于日志记录的静态Logger实例。
    /// </summary>
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger<WorkflowPersistenceService>();

    /// <summary>
    /// 提供对所有当前实例状态的只读访问。
    /// </summary>
    public async Task<Result<IReadOnlyDictionary<Guid, InstanceStateRecord>>> GetAllStatesAsync() =>
        await this.Storage.GetAllStatesAsync();

    /// <summary>
    /// 开始一个持久化操作的流畅构造过程。
    /// </summary>
    /// <param name="instanceId">操作的唯一、确定性ID。</param>
    /// <param name="inputs">操作的输入。</param>
    /// <returns>一个持久化调用构造器，用于指定并执行操作逻辑。</returns>
    public PersistenceCallBuilder<TInput> WithPersistence<TInput>(Guid instanceId, TInput inputs) => new(this, instanceId, inputs);

    /// <summary>
    /// 异步获取指定实例ID的状态记录。
    /// </summary>
    /// <param name="instanceId">实例的唯一ID。</param>
    /// <returns>一个 Result 对象，成功时包含找到的状态记录 (可能为 null)，失败时包含错误。</returns>
    public async Task<Result<InstanceStateRecord?>> GetStateAsync(Guid instanceId) => await this.Storage.GetStateAsync(instanceId);


    /// <summary>
    /// 将实例状态设置为 Running。
    /// </summary>
    public async Task SetRunningStateAsync<TInput>(Guid instanceId, TInput effectiveInputs)
    {
        var runningState = RunningState.Create(instanceId, effectiveInputs);
        var setStateResult = await this.Storage.SetStateAsync(runningState);
        if (setStateResult.TryGetError(out var setStateError))
        {
            Logger.Error(setStateError);
        }

        Logger.Debug("持久化更新 (Running): InstanceId={InstanceId}。", instanceId);
    }

    /// <summary>
    /// 保存成功完成的状态。
    /// </summary>
    public async Task SaveCompletedStateAsync<TInput,TPayload>(Guid instanceId, TInput inputs, TPayload payload)
    {
        var completedState = CompletedState.Create(instanceId, inputs, payload);
        var setStateResult = await this.Storage.SetStateAsync(completedState);
        if (setStateResult.TryGetError(out var setStateError))
        {
            Logger.Error(setStateError);
        }

        Logger.Debug("持久化更新 (Completed): InstanceId={InstanceId}。执行成功并保存结果。", instanceId);
    }

    /// <summary>
    /// 保存失败的状态。
    /// </summary>
    public async Task SaveFailedStateAsync<TInput>(Guid instanceId, TInput inputs, Error error)
    {
        var failedState = FailedState.Create(instanceId, inputs, error);
        var setStateResult = await this.Storage.SetStateAsync(failedState);
        if (setStateResult.TryGetError(out var setStateError))
        {
            Logger.Error(setStateError);
        }

        Logger.Error("持久化更新 (Failed): InstanceId={InstanceId}。执行失败并保存错误。{ErrorDetail}", instanceId, error.ToDetailString());
    }
}
