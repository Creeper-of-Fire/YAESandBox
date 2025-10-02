using YAESandBox.Depend.AspNetCore;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.Config.Stored;

namespace YAESandBox.Workflow.API;

/// <summary>
/// 标记一个模块，表明它能提供一个或多个“官方”的、只读的内部配置。
/// 模块可以根据需要实现部分或全部方法。
/// </summary>
public interface IProgramModuleInnerConfigProvider : IProgramModule
{
    /// <summary>
    /// 获取该模块提供的所有内部工作流 (Workflow) 配置。
    /// </summary>
    IReadOnlyList<StoredConfig<WorkflowConfig>> GetWorkflowInnerConfigs();

    /// <summary>
    /// 获取该模块提供的所有内部枢机 (Tuum) 配置。
    /// </summary>
    IReadOnlyList<StoredConfig<TuumConfig>> GetTuumInnerConfigs();

    /// <summary>
    /// 获取该模块提供的所有内部符文 (Rune) 配置。
    /// </summary>
    IReadOnlyList<StoredConfig<AbstractRuneConfig>> GetRuneInnerConfigs();
}