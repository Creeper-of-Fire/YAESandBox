namespace YAESandBox.Workflow.DebugDto;

/// <inheritdoc />
public interface IWorkflowProcessorDebugDto : IDebugDto
{
    /// <summary>
    /// 工作流中所有枢机的调试信息
    /// </summary>
    IList<ITuumProcessorDebugDto> TuumProcessorDebugDtos { get; }
}