namespace YAESandBox.Workflow.DebugDto;

/// <inheritdoc />
public interface IWorkflowProcessorDebugDto : IDebugDto
{
    /// <summary>
    /// 工作流中所有步骤的调试信息
    /// </summary>
    IList<IStepProcessorDebugDto> StepProcessorDebugDtos { get; }
}