using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.Runtime.RuntimePersistence;

/// <summary>
/// 一个流畅的构造器，用于分阶段配置并构建一个持久化操作。
/// </summary>
/// <typeparam name="TInput">操作的输入类型。</typeparam>
public class PersistenceCallBuilder<TInput>(
    WorkflowPersistenceService persistenceService,
    Guid instanceId,
    TInput inputs)
{
    private WorkflowPersistenceService PersistenceService { get; } = persistenceService;
    private Guid InstanceId { get; } = instanceId;
    private TInput Inputs { get; } = inputs;


    /// <summary>
    /// 指定一个操作逻辑并构建可执行的操作。
    /// </summary>
    /// <param name="executionLogic">核心业务逻辑。</param>
    /// <returns>一个已配置完毕、可执行的持久化操作实例。</returns>
    public PersistenceOperation<TInput, TPayload> ExecuteAsync<TPayload>(Func<TInput, Task<Result<TPayload>>> executionLogic)
    {
        return new PersistenceOperation<TInput, TPayload>(
            this.PersistenceService,
            this.InstanceId,
            this.Inputs,
            executionLogic
        );
    }

    /// <summary>
    /// 指定一个【没有输出】的操作逻辑并构建可执行的操作。
    /// </summary>
    /// <param name="executionLogic">核心业务逻辑。</param>
    /// <returns>一个已配置完毕、可执行的持久化操作实例。</returns>
    public PersistenceOperation<TInput, object?> ExecuteAsync(Func<TInput, Task<Result>> executionLogic)
    {
        return new PersistenceOperation<TInput, object?>(
            this.PersistenceService,
            this.InstanceId,
            this.Inputs,
            AdaptedLogic
        );

        // 将 Func<..., Task<Result>> 适配为 Func<..., Task<Result<object?>>>
        async Task<Result<object?>> AdaptedLogic(TInput input)
        {
            var result = await executionLogic(input);
            if (result.TryGetError(out var error))
                return error;

            return Result.Ok<object?>(null);
        }
    }
}