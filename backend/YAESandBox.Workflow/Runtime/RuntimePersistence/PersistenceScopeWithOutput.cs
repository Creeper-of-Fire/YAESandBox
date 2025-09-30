using System.Diagnostics.CodeAnalysis;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Workflow.Runtime.RuntimePersistence;

/// <summary>
/// 管理单个持久化操作的生命周期。
/// 使用 `await using` 语法来确保操作的开始和结束状态被正确处理。
/// </summary>
/// <typeparam name="TInput">操作的输入类型。</typeparam>
/// <typeparam name="TOutput">操作的输出类型。</typeparam>
public abstract class PersistenceScopeWithOutputBase<TInput, TOutput> : PersistenceScopeBase<TInput>
{
    /// <summary>
    /// 最终的结果。
    /// </summary>
    protected Result<TOutput>? FinalResult { get; set; }

    /// <summary>
    /// 序列化后的输出。
    /// </summary>
    protected string? SerializedOutputs { get; }

    /// <summary>
    /// 被缓存的错误。
    /// </summary>
    protected Error? CachedError { get; }

    /// <summary>
    /// 【仅当 ShouldExecute 为 false 时可用】
    /// 如果缓存命中，此属性包含缓存的结果。
    /// 在 ShouldExecute 为 true 时访问此属性将抛出 InvalidScopeOperationException。
    /// </summary>
    public abstract Result<TOutput> CachedResult { get; }

    internal PersistenceScopeWithOutputBase(
        WorkflowPersistenceService persistenceService,
        Guid instanceId,
        bool shouldExecute,
        TInput effectiveInputs,
        string serializedInputs,
        string? serializedOutputs,
        Error? cachedError
    )
        : base(persistenceService, instanceId, shouldExecute, effectiveInputs, serializedInputs)
    {
        if (!shouldExecute && cachedError is null && serializedOutputs is null)
            throw new InvalidOperationException("缓存命中时，必须提供 serializedOutputs 或 cachedError。");

        this.CachedError = cachedError;
        this.SerializedOutputs = serializedOutputs;
    }

    /// <summary>
    /// 设置最终的执行结果。
    /// </summary>
    public virtual void SetResult(Result<TOutput> result)
    {
        this.FinalResult = result;
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (!this.ShouldExecute) return;

        var resultToSave = this.FinalResult ?? new Error($"InstanceId 为 '{this.InstanceId}' 的持久化作用域在未设置结果时就被释放了。");

        if (resultToSave.TryGetValue(out var output, out var error))
        {
            await this.PersistenceService.SaveCompletedStateAsync(this.InstanceId, this.SerializedInputs, output);
        }
        else
        {
            await this.PersistenceService.SaveFailedStateAsync(this.InstanceId, this.SerializedInputs, error);
        }
    }
}

/// <summary>
/// 管理一个保证其输出 TOutput 永远不为 null 的持久化操作。
/// </summary>
/// <typeparam name="TInput">操作的输入类型。</typeparam>
/// <typeparam name="TOutput">约束为 notnull (class 或 non-nullable struct)。</typeparam>
public sealed class PersistenceScopeWithNonNullOutput<TInput, TOutput>
    : PersistenceScopeWithOutputBase<TInput, TOutput> where TOutput : notnull
{
    /// <inheritdoc />
    [field: AllowNull, MaybeNull]
    public override Result<TOutput> CachedResult
    {
        get
        {
            if (this.ShouldExecute)
                throw new InvalidScopeOperationException("不能在需要执行的作用域 (ShouldExecute is true) 中访问 CachedResult。");
            if (field is not null)
                return field;
            // 如果是失败状态，直接构建失败结果
            if (this.CachedError is not null)
            {
                field = this.CachedError;
                return field;
            }

            // TODO 考虑当this.SerializedOutputs为空时运行而非异常
            if (this.SerializedOutputs is null)
            {
                throw new ArgumentNullException(nameof(this.SerializedOutputs), "SerializedOutputs 为 null，请检查持久化缓存。");
            }

            // 执行反序列化
            try
            {
                var deserializedValue = YaeSandBoxJsonHelper.Deserialize<TOutput?>(this.SerializedOutputs);
                if (deserializedValue is null)
                {
                    throw new ArgumentNullException(nameof(this.SerializedOutputs),
                        $"反序列化成功，但结果为 null，这对于不可为空的输出类型 '{typeof(TOutput).FullName}' 是无效的。");
                }

                field = Result.Ok(deserializedValue);
                return field;
            }
            catch (Exception ex)
            {
                // 如果反序列化失败，这是一个严重的问题，返回一个包含错误的 Result
                var error = new Error($"从持久化缓存中反序列化类型 '{typeof(TOutput).FullName}' 失败。", ex);
                field = error;
                return field;
            }
        }
    }

    internal PersistenceScopeWithNonNullOutput(
        WorkflowPersistenceService persistenceService,
        Guid instanceId,
        bool shouldExecute,
        TInput effectiveInputs,
        string serializedInputs,
        string? serializedOutputs,
        Error? cachedError
    ) : base(persistenceService, instanceId, shouldExecute, effectiveInputs, serializedInputs, serializedOutputs, cachedError) { }
}

/// <summary>
/// 管理一个允许其输出 TOutput 为 null 的持久化操作。
/// </summary>
/// <typeparam name="TInput">操作的输入类型。</typeparam>
/// <typeparam name="TOutput">操作的输出类型。</typeparam>
public sealed class PersistenceScopeWithOutput<TInput, TOutput>
    : PersistenceScopeWithOutputBase<TInput, TOutput?>
{
    /// <inheritdoc />
    [field: AllowNull, MaybeNull]
    public override Result<TOutput?> CachedResult
    {
        get
        {
            if (this.ShouldExecute)
                throw new InvalidScopeOperationException("不能在需要执行的作用域 (ShouldExecute is true) 中访问 CachedResult。");
            if (field is not null)
                return field;
            // 如果是失败状态，直接构建失败结果
            if (this.CachedError is not null)
            {
                field = this.CachedError;
                return field;
            }

            // 执行反序列化
            try
            {
                if (this.SerializedOutputs is null)
                {
                    field = Result.Ok<TOutput?>(default);
                    return field;
                }
                
                // 注意这里，我们反序列化为 TOutput? 来正确处理 JSON "null"
                var deserializedValue = YaeSandBoxJsonHelper.Deserialize<TOutput?>(this.SerializedOutputs);
                field = Result.Ok(deserializedValue);
                return field;
            }
            catch (Exception ex)
            {
                // 如果反序列化失败，这是一个严重的问题，返回一个包含错误的 Result
                var error = new Error($"从持久化缓存中反序列化类型 '{typeof(TOutput).FullName}' 失败。", ex);
                field = error;
                return field;
            }
        }
    }

    internal PersistenceScopeWithOutput(
        WorkflowPersistenceService persistenceService,
        Guid instanceId,
        bool shouldExecute,
        TInput effectiveInputs,
        string serializedInputs,
        string? serializedOutputs,
        Error? cachedError
    ) : base(persistenceService, instanceId, shouldExecute, effectiveInputs, serializedInputs, serializedOutputs, cachedError) { }
}