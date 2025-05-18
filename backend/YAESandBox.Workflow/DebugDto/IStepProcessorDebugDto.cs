using YAESandBox.Workflow.Module;

namespace YAESandBox.Workflow.DebugDto;

/// <inheritdoc />
public interface IStepProcessorDebugDto : IDebugDto
{
    /// <summary>
    /// 下属的 <see cref="IModuleProcessor"/> 的Debug信息
    /// </summary>
    IList<IModuleProcessorDebugDto> ModuleProcessorDebugDtos { get; }
}