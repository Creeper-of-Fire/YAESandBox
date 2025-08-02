using YAESandBox.Depend.AspNetCore.PluginDiscovery;

namespace YAESandBox.AppWeb;


/// <summary>
/// IPluginAssetService 的默认实现。
/// </summary>
public class PluginAssetService : IPluginAssetService
{
    private IReadOnlyDictionary<string, PluginInfo> Plugins { get; init; }

    // TODO 这里的查找逻辑有点重复了，考虑再研究一下。
    
    /// <summary>
    /// 初始化 PluginAssetService。
    /// </summary>
    /// <param name="discoveryService">插件发现服务。</param>
    public PluginAssetService(IPluginDiscoveryService discoveryService)
    {
        var pluginDict = new Dictionary<string, PluginInfo>(StringComparer.OrdinalIgnoreCase);
        
        var discoveredPlugins = discoveryService.DiscoverPlugins();

        foreach (var plugin in discoveredPlugins)
        {
            var assetPaths = new List<string>();
            if (plugin.WwwRootPath is not null && Directory.Exists(plugin.WwwRootPath))
            {
                string[] files = Directory.GetFiles(plugin.WwwRootPath, "*", SearchOption.AllDirectories);
                assetPaths.AddRange(files.Select(file => Path.GetRelativePath(plugin.WwwRootPath, file).Replace('\\', '/')));
            }

            pluginDict[plugin.Name] = new PluginInfo
            {
                Name = plugin.Name,
                AssetPaths = assetPaths.AsReadOnly()
            };
        }
        this.Plugins = pluginDict;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PluginInfo> GetAllPlugins() => this.Plugins.Values.ToList();

    /// <inheritdoc />
    public PluginInfo? GetPlugin(string pluginName) => this.Plugins.GetValueOrDefault(pluginName);
}