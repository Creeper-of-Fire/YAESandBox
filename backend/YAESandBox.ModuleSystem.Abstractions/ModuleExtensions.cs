using YAESandBox.ModuleSystem.Abstractions.PluginDiscovery;

namespace YAESandBox.ModuleSystem.Abstractions;

/// <summary>
/// 模块系统的扩展方法
/// </summary>
public static class ModuleExtensions
{
    
    /// <summary>
    /// 获得模块对应的前端请求路径
    /// </summary>
    /// <param name="module"></param>
    /// <returns></returns>
    public static string ToRequestPath(this IProgramModule module) =>
        module is IYaeSandBoxPlugin plugin ? plugin.ToPluginRequestPath() : module.ToModuleRequestPath();

    private static string ToModuleRequestPath(this IProgramModule module) => $"/plugins/{module.GetType().Name}";

    private static string ToPluginRequestPath(this IYaeSandBoxPlugin plugin) => $"/plugins/{plugin.Metadata.Id}";
}