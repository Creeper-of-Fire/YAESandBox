namespace YAESandBox.ModuleSystem.Abstractions.PluginDiscovery;

/// <summary>
/// 插件的元数据
/// </summary>
/// <param name="Id"></param>
/// <param name="Name"></param>
/// <param name="Version"></param>
/// <param name="Author"></param>
/// <param name="Description"></param>
public record PluginMetadata(string Id, string Name, string Version,string Author,string Description);

/// <summary>
/// 插件的定义
/// </summary>
public interface IYaeSandBoxPlugin : IProgramModule
{
    /// <summary>
    /// 插件的元数据
    /// </summary>
    public PluginMetadata Metadata { get; }
}