using YAESandBox.Workflow.Module;

namespace YAESandBox.Workflow;

internal static class ConfigLocator
{
    internal static WorkflowProcessorConfig FindWorkflowProcessorConfig(string workflowId)
    {
        throw new NotImplementedException();
    }

    internal static StepProcessorConfig FindStepProcessorConfig(string stepId)
    {
        throw new NotImplementedException();
    }

    internal static AbstractModuleConfig<IWorkflowModule> FindModuleConfig(string moduleId)
    {
        throw new NotImplementedException();
    }
}