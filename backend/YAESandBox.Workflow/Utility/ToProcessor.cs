using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Step;

namespace YAESandBox.Workflow.Utility;

// TODO 复杂化、并行化、变量传递连连看……
// 输入输出变量检测：只在配置时检测，在转换为Processor时不检测。又或者在连连看生成时检测。
// 现在由前端直接把完整的Config发给后端，而不是在构建/同步前端的变动
internal static class ToProcessor
{
    internal static StepProcessor ToStepProcessor(this StepProcessorConfig stepProcessorConfig,
        WorkflowProcessor.WorkflowProcessorContent workflowProcessorContent, Dictionary<string, object> stepInput)
    {
        return new StepProcessor(workflowProcessorContent, stepProcessorConfig,
            stepProcessorConfig.Modules.ConvertAll(module => module.ToModuleProcessor()), stepInput);
    }

    internal static WorkflowProcessor ToWorkflowProcessor(
        this WorkflowProcessorConfig workflowProcessorConfig,
        IReadOnlyDictionary<string, string> triggerParams,
        IMasterAiService masterAiService,
        IWorkflowDataAccess dataAccess,
        Action<DisplayUpdateRequestPayload> requestDisplayUpdateCallback)
    {
        var content = new WorkflowProcessor.WorkflowProcessorContent(masterAiService, dataAccess, requestDisplayUpdateCallback);
        var variables = triggerParams.ToDictionary(kv => kv.Key, object (kv) => kv.Value);

        return new WorkflowProcessor(content, workflowProcessorConfig.Steps.ConvertAll(it => it.ToStepProcessor(content, variables)),
            variables);
    }
}