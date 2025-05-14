using YAESandBox.Workflow.Module;

namespace YAESandBox.Workflow;

internal static class ConfigLocator
{
    internal static WorkflowProcessorConfig findWorkflowProcessorConfig(string workflowId)
    {
        throw new NotImplementedException();
    }

    internal static StepProcessorConfig findStepProcessorConfig(string stepID)
    {
        throw new NotImplementedException();
    }

    internal static AbstractModuleConfig<IWorkflowModule> findModuleConfig(string moduleID)
    {
        throw new NotImplementedException();
    }
}