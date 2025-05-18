using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Module;
using YAESandBox.Workflow.Step;

namespace YAESandBox.Workflow;

public class WorkflowConfigService(IGeneralJsonStorage generalJsonStorage)
{
    private IGeneralJsonStorage GeneralJsonStorage { get; } = generalJsonStorage;

    public static WorkflowProcessorConfig FindWorkflowProcessorConfig(string workflowId)
    {
        throw new NotImplementedException();
    }

    public static StepProcessorConfig FindStepProcessorConfig(string stepId)
    {
        throw new NotImplementedException();
    }

    public static IModuleConfig FindModuleConfig(string moduleId)
    {
        throw new NotImplementedException();
    }

    public static void SaveModuleConfig(string moduleId, IModuleConfig moduleConfig)
    {
        throw new NotImplementedException();
    }

    public static void SaveWorkflowProcessorConfig(string workflowId, WorkflowProcessorConfig workflowProcessorConfig)
    {
        throw new NotImplementedException();
    }

    public static void SaveStepProcessorConfig(string stepId, StepProcessorConfig stepProcessorConfig)
    {
        throw new NotImplementedException();
    }
}