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
    public SubAiService AiService { get; } = aiService;
    public IWorkflowDataAccess DataAccess { get; } = dataAccess;
    public IWorkflowCallback Callback { get; } = callback;
}