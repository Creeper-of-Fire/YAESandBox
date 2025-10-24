using YAESandBox.Workflow.Core.Config.RuneConfig;

namespace YAESandBox.Workflow.Core.DebugDto;

/// <inheritdoc />
public interface ITuumProcessorDebugDto : IDebugDto
{
    /// <summary>
    /// 下属的 <see cref="AbstractRuneConfig{TProcessor}"/> 的Debug信息
    /// </summary>
    IList<IRuneProcessorDebugDto> RuneProcessorDebugDtos { get; }
}