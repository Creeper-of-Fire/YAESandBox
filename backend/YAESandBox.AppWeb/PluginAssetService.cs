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
    /// <param name="environment"></param>
    /// <param name="configuration">应用程序配置。</param>
    public PluginAssetService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var pluginDict = new Dictionary<string, PluginInfo>(StringComparer.OrdinalIgnoreCase);
        
        string pluginsRelativePath = configuration.GetValue<string>("Plugins:RootPath") ?? "Plugins";
        string pluginsRootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, pluginsRelativePath));
        if (!Directory.Exists(pluginsRootPath))
        {
            this.Plugins = pluginDict;
            return;
        }

        foreach (string pluginDir in Directory.GetDirectories(pluginsRootPath))
        {
            string pluginName = new DirectoryInfo(pluginDir).Name;
            string wwwrootPath = Path.Combine(pluginDir, "wwwroot");
            
            var assetPaths = new List<string>();
            if (Directory.Exists(wwwrootPath))
            {
                string[] files = Directory.GetFiles(wwwrootPath, "*", SearchOption.AllDirectories);
                assetPaths.AddRange(files.Select(file => Path.GetRelativePath(wwwrootPath, file).Replace('\\', '/')));
            }

            pluginDict[pluginName] = new PluginInfo
            {
                Name = pluginName,
                AssetPaths = assetPaths.AsReadOnly()
            };
        }
        this.Plugins = pluginDict;
        Console.WriteLine($"[PluginAssetService] 初始化完成, 发现 {this.Plugins.Count} 个插件。");
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PluginInfo> GetAllPlugins() => this.Plugins.Values.ToList();

    /// <inheritdoc />
    public PluginInfo? GetPlugin(string pluginName) => this.Plugins.GetValueOrDefault(pluginName);
}