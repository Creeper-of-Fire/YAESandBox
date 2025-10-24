using System.Reflection;

namespace YAESandBox.ModuleSystem.Abstractions;

/// <summary>
/// 为模块初始化提供所需的上下文信息。
/// </summary>
/// <param name="AllModules">所有已发现的模块实例的只读列表。</param>
/// <param name="PluginAssemblies">所有已加载的插件程序集的只读列表。</param>
public record ModuleInitializationContext(
    IReadOnlyList<IProgramModule> AllModules,
    IReadOnlyList<Assembly> PluginAssemblies
);

/// <summary>
/// 标记一个模块，表明它需要在所有模块被发现后进行一次性的初始化。
/// </summary>
public interface IProgramModuleWithInitialization : IProgramModule
{
    /// <summary>
    /// 在所有模块被发现后执行初始化逻辑。
    /// </summary>
    /// <param name="context">包含了初始化所需信息的上下文对象。</param>
    void Initialize(ModuleInitializationContext context);
}