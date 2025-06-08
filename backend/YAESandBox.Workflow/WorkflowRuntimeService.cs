using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.AIService;

namespace YAESandBox.Workflow;

/// <summary>
/// 封装工作流执行所需的无状态、只读的外部服务。
/// 这个对象在工作流启动时创建一次，并被传递给所有需要它的组件。
/// </summary>
/// <param name="masterAiService"></param>
/// <param name="dataAccess"></param>
/// <param name="callback"></param>
public class WorkflowRuntimeService(
    IMasterAiService masterAiService,
    IWorkflowDataAccess dataAccess,
    IWorkflowCallback callback)
{
    public IMasterAiService MasterAiService { get; } = masterAiService;
    public IWorkflowDataAccess DataAccess { get; } = dataAccess;
    public IWorkflowCallback Callback { get; } = callback;
}