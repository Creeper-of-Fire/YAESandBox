using System.ComponentModel.DataAnnotations;

namespace YAESandBox.Workflow.Module;

public abstract record AbstractModuleConfig<T>([Required] string ModuleType)
    where T : IWorkflowModule
{
    internal IWorkflowModule ToModule(StepProcessor.StepProcessorContent stepProcessor) =>
        this.ToCurrentModule(stepProcessor);

    internal abstract T ToCurrentModule(StepProcessor.StepProcessorContent stepProcessor);
}

public interface IWorkflowModule
{
    // Task ExecuteAsync(dynamic input,dynamic output,);
}