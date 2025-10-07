using System.Diagnostics;
using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;

namespace YAESandBox.Workflow.Runtime.RuntimePersistence;

/// <summary>
/// 代表一个已完全配置、待执行的持久化操作。
/// 此类封装了“决策 -> 执行 -> 保存”的完整模板逻辑。
/// </summary>
/// <typeparam name="TInput">操作的输入类型。</typeparam>
/// <typeparam name="TPayload">操作的输出类型。</typeparam>
public sealed class PersistenceOperation<TInput, TPayload>(
    WorkflowPersistenceService persistenceService,
    Guid instanceId,
    TInput inputs,
    Func<TInput, Task<Result<TPayload>>> executionLogic)
{
    private WorkflowPersistenceService PersistenceService { get; } = persistenceService;
    private Guid InstanceId { get; } = instanceId;
    private TInput Inputs { get; } = inputs;
    private Func<TInput, Task<Result<TPayload>>> ExecutionLogic { get; } = executionLogic;

    private Func<TInput, TPayload, Task>? OnCachedSuccessAction { get; set; }

    private Func<TInput, Error, Task>? OnCachedFailureAction { get; set; }
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger<PersistenceOperation<TInput, TPayload>>();


    /// <summary>
    /// 注册一个【仅在操作从缓存中成功恢复时】调用的委托。
    /// <para>
    /// 此钩子是处理缓存命中场景的专用机制，例如：执行一个不需要重复计算但每次都需要触发的简单副作用（如UI更新、记录日志）。
    /// 对于需要【在任何失败情况下（无论是新执行还是缓存恢复）】都运行的逻辑，请使用返回的 Task&lt;Result&gt; 进行后续操作。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para><b>警告：</b>此钩子内的逻辑仅在缓存命中时执行，这会使工作流的首次执行与后续执行在可观察行为上产生差异。</para>
    /// <para>为了维护核心工作流的确定性，关键业务逻辑不应置于此钩子内。请谨慎使用。</para>
    /// </remarks>
    /// <param name="action">接收【已缓存的输入】和【已缓存的载荷】作为参数的异步委托。</param>
    /// <returns>当前操作实例，以支持链式调用。</returns>
    public PersistenceOperation<TInput, TPayload> WhenCachedSuccess(Func<TInput, TPayload, Task> action)
    {
        this.OnCachedSuccessAction = action;
        return this;
    }

    /// <summary>
    /// 注册一个【仅在操作从缓存中恢复一个失败状态时】调用的委托。
    /// <para>
    /// 此钩子用于对已知的、持久化的失败状态做出反应，例如：每次访问都尝试记录一次警报。
    /// 对于任需要【在任何失败情况下（无论是新执行还是缓存恢复）】都运行的逻辑，请使用返回的 Task&lt;Result&gt; 进行后续操作。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para><b>警告：</b>此钩子内的逻辑仅在缓存命中时执行，这会使工作流的首次执行与后续执行在可观察行为上产生差异。</para>
    /// <para>为了维护核心工作流的确定性，关键业务逻辑不应置于此钩子内。请谨慎使用。</para>
    /// </remarks>
    /// <param name="action">接收【已缓存的输入】和【已缓存的错误】作为参数的异步委托。</param>
    /// <returns>当前操作实例，以支持链式调用。</returns>
    public PersistenceOperation<TInput, TPayload> WhenCachedFailure(Func<TInput, Error, Task> action)
    {
        this.OnCachedFailureAction = action;
        return this;
    }

    /// <summary>
    /// 异步执行此持久化操作。
    /// </summary>
    /// <returns>操作的最终结果，可能来自缓存或新执行。</returns>
    public async Task<Result<TPayload>> RunAsync()
    {
        return await this.DecideAndRun(this.InstanceId, this.Inputs);
    }

    private async Task<Result<TPayload>> DecideAndRun(
        Guid instanceId, TInput currentInputs)
    {
        var existingStateResult = await this.PersistenceService.GetStateAsync(instanceId);
        if (existingStateResult.TryGetError(out var existingStateError, out var existingState))
        {
            // 如果存储层本身出错了，我们需要处理它
            // 在这里，我们可以简单地抛出异常或继续执行，具体取决于策略
            // 为了健壮性，我们假设可以继续
            Logger.Error(existingStateError);
            existingState = null;
        }

        if (existingState is null)
        {
            // 状态不存在，必须执行
            return await this.RunExecute(currentInputs);
        }

        switch (existingState)
        {
            case CompletedState completedState:
                Logger.Debug("持久化命中 (Completed): InstanceId={InstanceId}。返回缓存结果。", instanceId);
                return await this.RunCachedSuccess(completedState);

            case FailedState failedState:
                Logger.Warn("持久化命中 (Failed): InstanceId={InstanceId}。返回缓存的错误。", instanceId);
                return await this.RunCachedFailure(failedState);

            case RunningState runningState:
                Logger.Info("持久化恢复 (Running): InstanceId={InstanceId}。尝试恢复输入并继续执行。", instanceId);
                try
                {
                    return await this.RunExecute(runningState.GetInput<TInput>());
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "为 InstanceId={InstanceId} 恢复输入失败，将使用当前输入。", instanceId);
                    return await this.RunExecute(currentInputs);
                }

            default:
                // 这个 default 分支理论上是不可达的，因为我们的体系是密封的
                throw new UnreachableException($"未知的持久化状态类型: {existingState.GetType().FullName}");
        }
    }

    /// <summary>
    /// 分支逻辑：执行核心业务逻辑并保存结果。
    /// </summary>
    private async Task<Result<TPayload>> RunExecute(TInput effectiveInputs)
    {
        // a. 标记为 Running
        // 注意：这里我们保存 effectiveInputs，它可能是恢复出来的，也可能是当前的。无论什么情况我们都保存一下，这样的话就同步了。
        await this.PersistenceService.SetRunningStateAsync(this.InstanceId, effectiveInputs);

        // b. 执行核心业务逻辑
        var result = await this.ExecutionLogic(effectiveInputs);

        // c. 保存最终状态
        if (result.TryGetValue(out var output, out var error))
        {
            await this.PersistenceService.SaveCompletedStateAsync(this.InstanceId, effectiveInputs, output);
        }
        else
        {
            await this.PersistenceService.SaveFailedStateAsync(this.InstanceId, effectiveInputs, error);
        }

        return result;
    }

    /// <summary>
    /// 分支逻辑：处理缓存的成功状态，触发钩子并返回缓存好的结果。
    /// </summary>
    private async Task<Result<TPayload>> RunCachedSuccess(CompletedState successState)
    {
        try
        {
            // 在返回缓存结果前，执行缓存成功钩子
            if (this.OnCachedSuccessAction is not null)
            {
                var cachedInput = successState.GetInput<TInput>();
                var cachedPayload = successState.GetPayload<TPayload>();
                await this.OnCachedSuccessAction(cachedInput, cachedPayload);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex,
                "持久化操作的 {HookName} 钩子中发生未捕获异常。InstanceId={InstanceId}", nameof(this.WhenCachedSuccess), this.InstanceId);
        }

        // 无论钩子是否成功，都返回原始的缓存结果
        return successState.GetPayload<TPayload>();
    }

    /// <summary>
    /// 分支逻辑：处理缓存的失败状态，触发钩子并返回缓存好的错误。
    /// </summary>
    private async Task<Result<TPayload>> RunCachedFailure(FailedState failureState)
    {
        try
        {
            // 在返回缓存结果前，执行缓存失败钩子
            if (this.OnCachedFailureAction is not null)
            {
                var cachedInput = failureState.GetInput<TInput>();
                await this.OnCachedFailureAction(cachedInput, failureState.ErrorDetails);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex,
                "持久化操作的 {HookName} 钩子中发生未捕获异常。InstanceId={InstanceId}", nameof(this.WhenCachedFailure), this.InstanceId);
        }

        return failureState.ErrorDetails;
    }
}