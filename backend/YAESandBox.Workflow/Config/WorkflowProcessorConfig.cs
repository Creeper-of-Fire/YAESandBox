using YAESandBox.Workflow.Step;

namespace YAESandBox.Workflow;

public record WorkflowProcessorConfig
{
    public List<StepProcessorConfig> Steps { get; init; } = [];
}