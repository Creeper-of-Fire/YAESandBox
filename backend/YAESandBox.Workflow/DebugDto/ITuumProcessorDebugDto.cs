using YAESandBox.Workflow.Config.RuneConfig;

namespace YAESandBox.Workflow.DebugDto;

/// <inheritdoc />
public interface ITuumProcessorDebugDto : IDebugDto
{
    /// <summary>
    /// 下属的 <see cref="AbstractRuneConfig{T}"/> 的Debug信息
    /// </summary>
    IList<IRuneProcessorDebugDto> RuneProcessorDebugDtos { get; }
}