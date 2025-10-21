using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Runtime.RuntimePersistence;
using YAESandBox.Workflow.WorkflowService.Abstractions;

namespace YAESandBox.Workflow.WorkflowService;

/// <summary>
/// 封装工作流执行所需的无状态、只读的外部服务。
/// 这个对象在工作流启动时创建一次，并被传递给所有需要它的组件。
/// </summary>
/// <param name="aiService"></param>
/// <param name="dataAccess"></param>
/// <param name="callback"></param>
/// <param name="persistenceService"></param>
public class WorkflowRuntimeService(
    SubAiService aiService,
    IWorkflowDataAccess dataAccess,
    IWorkflowCallback callback,
    WorkflowPersistenceService persistenceService,
    WorkflowConfigFindService findService,
    string userId
)
{
    /// <summary>
    /// 使用的AI服务
    /// </summary>
    public SubAiService AiService { get; } = aiService;

    /// <summary>
    /// 数据访问服务
    /// </summary>
    public IWorkflowDataAccess DataAccess { get; } = dataAccess;

    /// <summary>
    /// 回调服务
    /// </summary>
    public IWorkflowCallback Callback { get; } = callback;

    /// <summary>
    /// 工作流持久化服务
    /// </summary>
    public WorkflowPersistenceService PersistenceService { get; } = persistenceService;
    
    /// <summary>
    /// 提供查找已保存配置（包括内置配置）的服务。
    /// </summary>
    public WorkflowConfigFindService FindService { get; } = findService;

    /// <summary>
    /// 触发此工作流执行的用户的ID。
    /// </summary>
    public string UserId { get; } = userId;
}