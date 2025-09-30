using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.Core.Abstractions;
using YAESandBox.Workflow.Runtime;
using YAESandBox.Workflow.Runtime.RuntimePersistence;
using YAESandBox.Workflow.Tuum;

namespace YAESandBox.Workflow.Utility;

// TODO 复杂化、并行化、变量传递连连看……
// 输入输出变量检测：只在配置时检测，在转换为Processor时不检测。又或者在连连看生成时检测。
// 现在由前端直接把完整的Config发给后端，而不是在构建/同步前端的变动
internal static class ToProcessor
{
    internal static TuumProcessor ToTuumProcessor(this TuumConfig tuumConfig,
        ICreatingContext creatingContext)
    {
        return new TuumProcessor(tuumConfig, creatingContext);
    }

    internal static WorkflowProcessor ToWorkflowProcessor(
        this WorkflowConfig workflowConfig,
        Guid workflowRunnerId,
        IReadOnlyDictionary<string, string> workflowInputs,
        SubAiService aiService,
        IWorkflowDataAccess dataAccess,
        IWorkflowCallback callback,
        WorkflowPersistenceService persistenceService
    )
    {
        var workflowRuntimeService = new WorkflowRuntimeService(aiService, dataAccess, callback, persistenceService);
        var context = ProcessorContext.CreateRoot(workflowRunnerId, workflowRuntimeService);
        return new WorkflowProcessor(workflowConfig, context, workflowInputs.ToDictionary());
    }
}