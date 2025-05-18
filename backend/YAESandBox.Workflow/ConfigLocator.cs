using YAESandBox.Workflow.Module;
using YAESandBox.Workflow.Step;

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

    internal static AbstractModuleConfig<IModuleProcessor> FindModuleConfig(string moduleId)
    {
        throw new NotImplementedException();
    }
}