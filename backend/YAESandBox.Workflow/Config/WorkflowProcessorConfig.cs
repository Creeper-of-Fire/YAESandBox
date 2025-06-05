namespace YAESandBox.Workflow.Config;

public record WorkflowProcessorConfig
{
    public List<StepProcessorConfig> Steps { get; init; } = [];
}