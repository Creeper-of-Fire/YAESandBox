using Microsoft.AspNetCore.Hosting;

namespace YAESandBox.Depend.AspNetCore.PluginDiscovery;

/// <summary>
/// 提供查询已发现插件及其静态资源信息的功能。
/// 此服务在程序启动时被填充，并在整个应用程序生命周期内提供只读访问。
/// </summary>
public interface IPluginAssetService
{
    /// <summary>
    /// 获取所有已发现的插件的信息。
    /// </summary>
    /// <returns>所有插件元数据的只读集合。</returns>
    IReadOnlyCollection<PluginInfo> GetAllPlugins();

    /// <summary>
    /// 根据插件名称查找插件信息。
    /// </summary>
    /// <param name="pluginName">插件的名称 (通常是文件夹名)。</param>
    /// <returns>找到的插件信息，如果不存在则返回 null。</returns>
    PluginInfo? GetPlugin(string pluginName);
}

/// <summary>
/// 封装了一个已发现插件的所有相关信息。
/// </summary>
public record PluginInfo
{
    /// <summary>
    /// 插件的名称，与插件目录名一致。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 插件 wwwroot 目录下的所有文件的相对路径列表。
    /// 例如: ["vue-bundle.js", "vue-bundle.css", "images/icon.png"]
    /// </summary>
    public IReadOnlyCollection<string> AssetPaths { get; init; } = [];

    /// <summary>
    /// 检查指定的资源是否存在于此插件中。
    /// </summary>
    /// <param name="assetPath">资源的相对路径，例如 "vue-bundle.css"。</param>
    /// <returns>如果存在则为 true，否则为 false。</returns>
    public bool HasAsset(string assetPath) => this.AssetPaths.Contains(assetPath, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取指定资源的公共URL。
    /// </summary>
    /// <param name="assetPath">资源的相对路径。</param>
    /// <returns>一个可公开访问的URL。</returns>
    public string GetAssetUrl(string assetPath) => $"/plugins/{this.Name}/{assetPath}";
}