using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.Runtime.RuntimePersistence;

/// <summary>
/// 管理无返回值 (void) 的持久化操作的生命周期。
/// 最终结果是 Result 类型。
/// </summary>
public sealed class PersistenceScopeWithoutOutput<TInput> : PersistenceScopeBase<TInput>
{
    private Result? FinalResult { get; set; }

    /// <summary>
    /// 【仅当 ShouldExecute 为 false 时可用】
    /// 缓存的执行结果。
    /// </summary>
    public Result CachedResult { get; }

    internal PersistenceScopeWithoutOutput(
        WorkflowPersistenceService persistenceService, Guid instanceId, bool shouldExecute,
        TInput effectiveInputs, string serializedInputs, Result? cachedResult)
        : base(persistenceService, instanceId, shouldExecute, effectiveInputs, serializedInputs)
    {
        if (!shouldExecute && cachedResult is null)
            throw new ArgumentNullException(nameof(cachedResult), "缓存命中时必须提供非空的 cachedResult。");

        this.CachedResult = cachedResult ?? Result.Ok();
    }

    /// <summary>
    /// 设置最终的执行结果。
    /// </summary>
    public void SetResult(Result result)
    {
        this.FinalResult = result;
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (!this.ShouldExecute) return;

        var resultToSave = this.FinalResult ??
                           new Error($"持久化作用域 for InstanceId '{this.InstanceId}' was disposed without a result being set.");

        if (resultToSave.TryGetError(out var error))
        {
            await this.PersistenceService.SaveFailedStateAsync(this.InstanceId, this.SerializedInputs, error);
        }
        else
        {
            // No output to save
            await this.PersistenceService.SaveCompletedStateAsync<object?>(this.InstanceId, this.SerializedInputs, null);
        }
    }
}