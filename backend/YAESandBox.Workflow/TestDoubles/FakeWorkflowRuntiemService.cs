using System.Diagnostics.CodeAnalysis;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Runtime.RuntimePersistence;
using YAESandBox.Workflow.Runtime.RuntimePersistence.Storage;
using YAESandBox.Workflow.WorkflowService;
using YAESandBox.Workflow.WorkflowService.Abstractions;

namespace YAESandBox.Workflow.TestDoubles;

/// <summary>
/// 一个用于单元测试的、纯粹的“空” WorkflowRuntimeService 实现。
/// 它的所有服务默认都是无操作的或内存中的实例，以确保测试的隔离性。
/// 属性是可设置的，以便在需要时注入特定的 Mock 对象。
/// </summary>
public sealed class FakeWorkflowRuntimeService() : WorkflowRuntimeService(
    aiService: new SubAiService(null!, ""),
    dataAccess: new FakeWorkflowDataAccess(),
    callback: new FakeWorkflowCallback(),
    persistenceService: new WorkflowPersistenceService(new InMemoryPersistenceStorage())
)
{
    /// <summary>
    /// （可选）允许在测试中替换 AI 服务。
    /// </summary>
    [field: AllowNull, MaybeNull]
    public new SubAiService AiService
    {
        get => field ?? base.AiService;
        set;
    }

    /// <summary>
    /// （可选）允许在测试中替换数据访问服务。
    /// </summary>
    [field: AllowNull, MaybeNull]
    public new IWorkflowDataAccess DataAccess
    {
        get => field ?? base.DataAccess;
        set;
    }

    /// <summary>
    /// （可选）允许在测试中替换回调服务。
    /// </summary>
    [field: AllowNull, MaybeNull]
    public new IWorkflowCallback Callback
    {
        get => field ?? base.Callback;
        set;
    }
}

/// <summary>
/// IWorkflowDataAccess 的一个空实现。
/// </summary>
file sealed class FakeWorkflowDataAccess : IWorkflowDataAccess
{
    // No methods to implement
}

/// <summary>
/// IWorkflowCallback 的一个空实现。
/// 这个实现不实现任何具体的回调接口（如 IWorkflowEventEmitter），
/// 因此对它的 GetWorkflowCallback&gt;T&lt;() 调用总是会失败，
/// 除非在测试中被替换为一个具体的 Mock。
/// </summary>
file sealed class FakeWorkflowCallback : IWorkflowCallback
{
    // No methods to implement
}