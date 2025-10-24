using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Core.Config;
using YAESandBox.Workflow.Core.Runtime.RuntimePersistence;
using YAESandBox.Workflow.Core.Runtime.WorkflowService;
using YAESandBox.Workflow.Core.Runtime.WorkflowService.Abstractions;

namespace YAESandBox.Workflow.Core.Runtime.Processor;

// TODO 复杂化、并行化、变量传递连连看……
// 输入输出变量检测：只在配置时检测，在转换为Processor时不检测。又或者在连连看生成时检测。
// 现在由前端直接把完整的Config发给后端，而不是在构建/同步前端的变动
/// <summary>
/// 将Config转为Processor的工具类
/// </summary>
public static class ToProcessor
{
    /// <summary>
    /// 将TuumConfig转为Processor
    /// </summary>
    /// <param name="tuumConfig"></param>
    /// <param name="creatingContext"></param>
    /// <returns></returns>
    public static TuumProcessor ToTuumProcessor(this TuumConfig tuumConfig,
        ICreatingContext creatingContext)
    {
        return new TuumProcessor(tuumConfig, creatingContext);
    }

    /// <summary>
    /// 将WorkflowConfig转为Processor
    /// </summary>
    public static WorkflowProcessor ToWorkflowProcessor(
        this WorkflowConfig workflowConfig,
        Guid workflowRunnerId,
        IReadOnlyDictionary<string, string> workflowInputs,
        SubAiService aiService,
        IWorkflowDataAccess dataAccess,
        IWorkflowCallback callback,
        WorkflowPersistenceService persistenceService,
        WorkflowConfigFindService workflowConfigFindService,
        string userId
    )
    {
        var workflowRuntimeService =
            new WorkflowRuntimeService(aiService, dataAccess, callback, persistenceService, workflowConfigFindService, userId);
        var context = ProcessorContext.CreateRoot(workflowRunnerId, workflowRuntimeService);
        return new WorkflowProcessor(workflowConfig, context, workflowInputs.ToDictionary());
    }
}