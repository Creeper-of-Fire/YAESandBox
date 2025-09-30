using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Core.Abstractions;

namespace YAESandBox.Workflow.Core;

/// <summary>
/// 封装工作流执行所需的无状态、只读的外部服务。
/// 这个对象在工作流启动时创建一次，并被传递给所有需要它的组件。
/// </summary>
/// <param name="aiService"></param>
/// <param name="dataAccess"></param>
/// <param name="callback"></param>
public class WorkflowRuntimeService(
    SubAiService aiService,
    IWorkflowDataAccess dataAccess,
    IWorkflowCallback callback)
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
}