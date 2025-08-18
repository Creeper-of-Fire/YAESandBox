namespace YAESandBox.Depend.AspNetCore.PluginDiscovery;

/// <summary>
/// 表示一个被发现的插件及其元数据。
/// </summary>
public record DiscoveredPlugin
{
    /// <summary>
    /// 插件的名称，通常是其所在的目录名。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 插件的物理根目录的完整路径。
    /// </summary>
    public required string PhysicalPath { get; init; }

    /// <summary>
    /// 插件目录中所有 DLL 文件的完整路径列表。
    /// </summary>
    public IReadOnlyList<string> DllPaths { get; init; } = [];
}

/// <summary>
/// 负责在应用程序启动时发现所有可用插件的服务。
/// 这个服务应该是单例的。
/// </summary>
public interface IPluginDiscoveryService
{
    /// <summary>
    /// 获取所有已发现的插件的只读集合。
    /// </summary>
    /// <returns>一个包含所有已发现插件信息的集合。</returns>
    IReadOnlyCollection<DiscoveredPlugin> DiscoverPlugins();
}