using YAESandBox.Workflow.DebugDto;

namespace YAESandBox.Workflow.Module;

/// <summary>
/// 模块配置的运行时
/// </summary>
public interface IModuleProcessor : IWithDebugDto<IModuleProcessorDebugDto>;