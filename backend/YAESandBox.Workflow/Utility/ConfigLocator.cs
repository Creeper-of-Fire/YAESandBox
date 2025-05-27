using YAESandBox.Workflow.Module;
using YAESandBox.Workflow.Step;

namespace YAESandBox.Workflow.Utility;

internal static class ConfigLocator
{
    internal static async Task<WorkflowProcessorConfig> FindWorkflowProcessorConfig(WorkflowConfigService workflowConfigService, string workflowId)
    {
        var result = await workflowConfigService.FindWorkflowConfig(workflowId);
        if (result.TryGetValue(out var value))
            return value;

        throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message);
    }

    internal static async Task<StepProcessorConfig> FindStepProcessorConfig(WorkflowConfigService workflowConfigService, string stepId)
    {
        var result = await workflowConfigService.FindStepConfig(stepId);
        if (result.TryGetValue(out var value))
            return value;
        
        throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message);
    }

    internal static async Task<IModuleConfig> FindModuleConfig(WorkflowConfigService workflowConfigService, string moduleId)
    {
        var result = await workflowConfigService.FindModuleConfig(moduleId);
        if (result.TryGetValue(out var value))
            return value;
        
        throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message);
    }
}