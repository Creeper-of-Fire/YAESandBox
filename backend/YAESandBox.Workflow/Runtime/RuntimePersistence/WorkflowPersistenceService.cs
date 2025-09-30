using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Runtime.RuntimePersistence.Storage;

namespace YAESandBox.Workflow.Runtime.RuntimePersistence;

/// <summary>
/// 定义持久化实例的状态。
/// </summary>
public enum PersistenceStatus
{
    /// <summary>
    /// 实例正在执行中。
    /// </summary>
    Running,

    /// <summary>
    /// 实例已成功完成。
    /// </summary>
    Completed,

    /// <summary>
    /// 实例执行失败。
    /// </summary>
    Failed
}

/// <summary>
/// 存储单个处理器实例的持久化状态的记录。
/// 这是一个内部记录，用于内存存储。
/// </summary>
/// <param name="InstanceId">实例的唯一ID。</param>
/// <param name="Status">实例的当前持久化状态。</param>
/// <param name="SerializedInputs">实例执行时的输入快照（序列化为JSON字符串）。</param>
/// <param name="SerializedOutputs">实例成功完成时的输出快照（序列化为JSON字符串）。</param>
/// <param name="ErrorDetails">实例失败时的错误信息。</param>
public record InstanceStateRecord(
    Guid InstanceId,
    PersistenceStatus Status,
    string? SerializedInputs,
    string? SerializedOutputs,
    Error? ErrorDetails
);

/// <summary>
/// 工作流持久化服务。
/// [注意]：此版本为纯内存实现，用于开发和测试。重启后数据会丢失。
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
    /// 创建并开始一个持久化执行作用域。
    /// 这是与持久化交互的主要入口点。
    /// 当作用域运行结束时，它会自动保存结果。
    /// </summary>
    /// <example>
    /// await using var scope = await persistenceService.CreateScope(id, inputs)
    /// 
    /// <p>if (!scope.ShouldExecute)
    ///     return scope.CachedResult;</p>
    ///     
    /// <p>var result = await MyLogic(scope.EffectiveInputs);</p>
    /// scope.SetResult(result); // 必须调用
    ///     
    /// <p>return result;</p>
    /// </example>
    public async Task<PersistenceScopeWithOutput<TInput, TOutput>> CreateScopeAsync<TInput, TOutput>(
        Guid instanceId,
        TInput inputs)
    {
        var effectiveInputs = inputs;

        // 1. 检查缓存
        var existingStateResult = await this.Storage.GetStateAsync(instanceId);
        if (existingStateResult.TryGetError(out var existingStateError, out var existingState))
        {
            // 如果存储层本身出错了，我们需要处理它
            // 在这里，我们可以简单地抛出异常或继续执行，具体取决于策略
            // 为了健壮性，我们假设可以继续
            Logger.Error(existingStateError);
            existingState = null;
        }

        if (existingState is not null)
        {
            switch (existingState.Status)
            {
                case PersistenceStatus.Completed:
                    Logger.Debug("持久化命中 (Completed): InstanceId={InstanceId}。返回缓存作用域。", instanceId);
                    return new PersistenceScopeWithOutput<TInput, TOutput>(this, instanceId, false, effectiveInputs,
                        existingState.SerializedInputs ?? "", existingState.SerializedOutputs, null);

                case PersistenceStatus.Failed:
                    Logger.Warn("持久化命中 (Failed): InstanceId={InstanceId}。返回缓存作用域。", instanceId);
                    return new PersistenceScopeWithOutput<TInput, TOutput>(this, instanceId, false, effectiveInputs,
                        existingState.SerializedInputs ?? "",
                        null,
                        existingState.ErrorDetails ?? new Error($"从持久化中恢复的未知错误 (InstanceId: {instanceId})"));

                case PersistenceStatus.Running:
                    Logger.Info("持久化恢复 (Running): InstanceId={InstanceId}。尝试恢复输入。", instanceId);
                    try
                    {
                        if (!string.IsNullOrEmpty(existingState.SerializedInputs))
                        {
                            var restoredInputs = YaeSandBoxJsonHelper.Deserialize<TInput>(existingState.SerializedInputs);
                            if (restoredInputs is not null) effectiveInputs = restoredInputs;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "为 InstanceId={InstanceId} 恢复输入失败，将使用当前输入。", instanceId);
                    }

                    break;
            }
        }

        // 2. 标记为 Running 并创建执行作用域
        string serializedInputs = YaeSandBoxJsonHelper.Serialize(effectiveInputs);
        var runningState = new InstanceStateRecord(instanceId, PersistenceStatus.Running, serializedInputs, null, null);
        var setStateResult = await this.Storage.SetStateAsync(runningState);
        if (setStateResult.TryGetError(out var setStateError))
        {
            Logger.Error(setStateError);
        }

        Logger.Debug("持久化更新 (Running): InstanceId={InstanceId}。返回执行作用域。", instanceId);

        return new PersistenceScopeWithOutput<TInput, TOutput>(
            this, instanceId, true, effectiveInputs, serializedInputs, null, null);
    }

    public async Task<PersistenceScopeWithoutOutput<TInput>> CreateScopeAsync<TInput>(
        Guid instanceId,
        TInput inputs)
    {
        var effectiveInputs = inputs;
        // 1. 检查缓存
        var existingStateResult = await this.Storage.GetStateAsync(instanceId);
        if (existingStateResult.TryGetError(out var existingStateError, out var existingState))
        {
            // 如果存储层本身出错了，我们需要处理它
            // 在这里，我们可以简单地抛出异常或继续执行，具体取决于策略
            // 为了健壮性，我们假设可以继续
            Logger.Error(existingStateError);
            existingState = null;
        }

        if (existingState is not null)
        {
            switch (existingState.Status)
            {
                case PersistenceStatus.Completed:
                    Logger.Debug("持久化命中 (Completed): InstanceId={InstanceId}。返回缓存作用域。", instanceId);
                    return new PersistenceScopeWithoutOutput<TInput>(this, instanceId, false, inputs,
                        existingState.SerializedInputs ?? "", Result.Ok());

                case PersistenceStatus.Failed:
                    Logger.Warn("持久化命中 (Failed): InstanceId={InstanceId}。返回缓存作用域。", instanceId);
                    return new PersistenceScopeWithoutOutput<TInput>(this, instanceId, false, inputs,
                        existingState.SerializedInputs ?? "",
                        Result.Fail(existingState.ErrorDetails ?? new Error($"从持久化中恢复的未知错误 (InstanceId: {instanceId})")));

                case PersistenceStatus.Running:
                    Logger.Info("持久化恢复 (Running): InstanceId={InstanceId}。尝试恢复输入。", instanceId);
                    try
                    {
                        if (!string.IsNullOrEmpty(existingState.SerializedInputs))
                        {
                            var restoredInputs = YaeSandBoxJsonHelper.Deserialize<TInput>(existingState.SerializedInputs);
                            if (restoredInputs is not null) effectiveInputs = restoredInputs;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "为 InstanceId={InstanceId} 恢复输入失败，将使用当前输入。", instanceId);
                    }

                    break;
            }
        }

        // 2. 标记为 Running 并创建执行作用域
        string serializedInputs = YaeSandBoxJsonHelper.Serialize(effectiveInputs);
        var runningState = new InstanceStateRecord(instanceId, PersistenceStatus.Running, serializedInputs, null, null);
        var setStateResult = await this.Storage.SetStateAsync(runningState);
        if (setStateResult.TryGetError(out var setStateError))
        {
            Logger.Error(setStateError);
        }

        Logger.Debug("持久化更新 (Running): InstanceId={InstanceId}。返回执行作用域。", instanceId);

        return new PersistenceScopeWithoutOutput<TInput>(this, instanceId, true, effectiveInputs, serializedInputs,
            Result.Fail("本错误理论上不会被使用。"));
    }

    public async Task<PersistenceScopeWithNonNullOutput<TInput, TOutput>> CreateScopeNotNullAsync<TInput, TOutput>(
        Guid instanceId,
        TInput inputs) where TOutput : notnull
    {
        var effectiveInputs = inputs;

        // 1. 检查缓存
        var existingStateResult = await this.Storage.GetStateAsync(instanceId);
        if (existingStateResult.TryGetError(out var existingStateError, out var existingState))
        {
            // 如果存储层本身出错了，我们需要处理它
            // 在这里，我们可以简单地抛出异常或继续执行，具体取决于策略
            // 为了健壮性，我们假设可以继续
            Logger.Error(existingStateError);
            existingState = null;
        }

        if (existingState is not null)
        {
            switch (existingState.Status)
            {
                case PersistenceStatus.Completed:
                    Logger.Debug("持久化命中 (Completed): InstanceId={InstanceId}。返回缓存作用域。", instanceId);
                    return new PersistenceScopeWithNonNullOutput<TInput, TOutput>(this, instanceId, false, effectiveInputs,
                        existingState.SerializedInputs ?? "", existingState.SerializedOutputs, null);

                case PersistenceStatus.Failed:
                    Logger.Warn("持久化命中 (Failed): InstanceId={InstanceId}。返回缓存作用域。", instanceId);
                    return new PersistenceScopeWithNonNullOutput<TInput, TOutput>(this, instanceId, false, effectiveInputs,
                        existingState.SerializedInputs ?? "",
                        null,
                        existingState.ErrorDetails ?? new Error($"从持久化中恢复的未知错误 (InstanceId: {instanceId})"));

                case PersistenceStatus.Running:
                    Logger.Info("持久化恢复 (Running): InstanceId={InstanceId}。尝试恢复输入。", instanceId);
                    try
                    {
                        if (!string.IsNullOrEmpty(existingState.SerializedInputs))
                        {
                            var restoredInputs = YaeSandBoxJsonHelper.Deserialize<TInput>(existingState.SerializedInputs);
                            if (restoredInputs is not null) effectiveInputs = restoredInputs;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "为 InstanceId={InstanceId} 恢复输入失败，将使用当前输入。", instanceId);
                    }

                    break;
            }
        }

        // 2. 标记为 Running 并创建执行作用域
        string serializedInputs = YaeSandBoxJsonHelper.Serialize(effectiveInputs);
        var runningState = new InstanceStateRecord(instanceId, PersistenceStatus.Running, serializedInputs, null, null);
        var setStateResult = await this.Storage.SetStateAsync(runningState);
        if (setStateResult.TryGetError(out var setStateError))
        {
            Logger.Error(setStateError);
        }

        Logger.Debug("持久化更新 (Running): InstanceId={InstanceId}。返回执行作用域。", instanceId);

        return new PersistenceScopeWithNonNullOutput<TInput, TOutput>(
            this, instanceId, true, effectiveInputs, serializedInputs, null, null);
    }

    /// <summary>
    /// 开始一个持久化操作的流畅构造过程。
    /// </summary>
    /// <param name="instanceId">操作的唯一、确定性ID。</param>
    /// <param name="inputs">操作的输入。</param>
    /// <returns>一个持久化调用构造器，用于指定并执行操作逻辑。</returns>
    public PersistenceCallBuilder<TInput> WithPersistence<TInput>(Guid instanceId, TInput inputs) => new(this, instanceId, inputs);

    // --- Internal Execution Logic ---
    // (These were the public methods, now they are internal and called by the builder)

    internal async Task<Result> ExecuteInternalAsync<TInput>(
        Guid instanceId, TInput inputs, Func<TInput, Task<Result>> executionLogic)
    {
        await using var scope = await this.CreateScopeAsync(instanceId, inputs);
        if (!scope.ShouldExecute) return scope.CachedResult;
        var result = await executionLogic(scope.EffectiveInputs);
        scope.SetResult(result);
        return result;
    }

    internal async Task<Result<TOutput?>> ExecuteInternalAsync<TInput, TOutput>(
        Guid instanceId, TInput inputs, Func<TInput, Task<Result<TOutput>>> executionLogic)
    {
        await using var scope = await this.CreateScopeAsync<TInput, TOutput>(instanceId, inputs);
        if (!scope.ShouldExecute)
            return scope.CachedResult;
        var result = await executionLogic(scope.EffectiveInputs);
        if (result.TryGetError(out var error, out var output))
        {
            return error;
        }

        scope.SetResult(output);
        return output;
    }

    internal async Task<Result<TOutput>> ExecuteNonNullInternalAsync<TInput, TOutput>(
        Guid instanceId, TInput inputs, Func<TInput, Task<Result<TOutput>>> executionLogic)
        where TOutput : notnull
    {
        await using var scope = await this.CreateScopeNotNullAsync<TInput, TOutput>(instanceId, inputs);
        if (!scope.ShouldExecute)
            return scope.CachedResult;
        var result = await executionLogic(scope.EffectiveInputs);
        scope.SetResult(result);
        return result;
    }

    // --- Internal methods for the Scope to call back ---

    internal async Task SaveCompletedStateAsync<TOutput>(Guid instanceId, string serializedInputs, TOutput output)
    {
        string serializedOutputs = YaeSandBoxJsonHelper.Serialize(output);
        var completedState = new InstanceStateRecord(instanceId, PersistenceStatus.Completed, serializedInputs, serializedOutputs, null);
        var setStateResult = await this.Storage.SetStateAsync(completedState);
        if (setStateResult.TryGetError(out var setStateError))
        {
            Logger.Error(setStateError);
        }

        Logger.Debug("持久化更新 (Completed): InstanceId={InstanceId}。执行成功并保存结果。", instanceId);
    }

    internal async Task SaveFailedStateAsync(Guid instanceId, string serializedInputs, Error error)
    {
        var failedState = new InstanceStateRecord(instanceId, PersistenceStatus.Failed, serializedInputs, null, error);
        var setStateResult = await this.Storage.SetStateAsync(failedState);
        if (setStateResult.TryGetError(out var setStateError))
        {
            Logger.Error(setStateError);
        }

        Logger.Error("持久化更新 (Failed): InstanceId={InstanceId}。执行失败并保存错误。{ErrorDetail}", instanceId, error.ToDetailString());
    }
}