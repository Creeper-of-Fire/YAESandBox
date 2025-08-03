using YAESandBox.Depend.AspNetCore;

namespace YAESandBox.Workflow.API;

/// <summary>
/// 标记一个模块，表明它能提供一个或多个工作流符文配置类型。
/// </summary>
public interface IProgramModuleRuneProvider : IProgramModule
{
    /// <summary>
    /// 获取该模块提供的所有符文配置 (AbstractRuneConfig) 类型。
    /// </summary>
    /// <returns>一个只读的符文配置类型列表。</returns>
    IReadOnlyList<Type> RuneConfigTypes { get; }
}