using YAESandBox.Workflow.Config;

namespace YAESandBox.Workflow.Utility;

internal static class ConfigLocator
{
    internal static async Task<WorkflowProcessorConfig> FindWorkflowProcessorConfig(WorkflowConfigFileService workflowConfigFileService,
        string workflowId)
    {
        var result = await workflowConfigFileService.FindWorkflowConfig(workflowId);
        if (result.TryGetValue(out var value))
            return value;

        throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message);
    }

    internal static async Task<StepProcessorConfig> FindStepProcessorConfig(WorkflowConfigFileService workflowConfigFileService,
        string stepId)
    {
        var result = await workflowConfigFileService.FindStepConfig(stepId);
        if (result.TryGetValue(out var value))
            return value;

        throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message);
    }

    internal static async Task<AbstractModuleConfig> FindModuleConfig(WorkflowConfigFileService workflowConfigFileService, string moduleId)
    {
        var result = await workflowConfigFileService.FindModuleConfig(moduleId);
        if (result.TryGetValue(out var value))
            return value;

        throw new InvalidOperationException(result.Errors.FirstOrDefault()?.Message);
    }
}