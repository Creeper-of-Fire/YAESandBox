using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Tuum;

namespace YAESandBox.Workflow.Utility;

// TODO 复杂化、并行化、变量传递连连看……
// 输入输出变量检测：只在配置时检测，在转换为Processor时不检测。又或者在连连看生成时检测。
// 现在由前端直接把完整的Config发给后端，而不是在构建/同步前端的变动
internal static class ToProcessor
{
    internal static TuumProcessor ToTuumProcessor(this TuumProcessorConfig tuumProcessorConfig,
        WorkflowRuntimeService workflowRuntimeService)
    {
        return new TuumProcessor(workflowRuntimeService, tuumProcessorConfig);
    }

    internal static WorkflowProcessor ToWorkflowProcessor(
        this WorkflowProcessorConfig workflowProcessorConfig,
        IReadOnlyDictionary<string, string> triggerParams,
        SubAiService aiService,
        IWorkflowDataAccess dataAccess,
        IWorkflowCallback callback)
    {
        var content = new WorkflowRuntimeService(aiService, dataAccess, callback);
        return new WorkflowProcessor(content, workflowProcessorConfig, triggerParams.ToDictionary());
    }
}