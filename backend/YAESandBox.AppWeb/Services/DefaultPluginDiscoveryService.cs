// 文件: YAESandBox.AppWeb/DefaultPluginDiscoveryService.cs

using System.Collections.ObjectModel;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;

namespace YAESandBox.AppWeb.Services;

/// <summary>
/// IPluginDiscoveryService 的默认实现。
/// 它在第一次被请求时扫描文件系统，然后缓存结果。
/// </summary>
public class DefaultPluginDiscoveryService : IPluginDiscoveryService
{
    private ReadOnlyCollection<DiscoveredPlugin> DiscoveredPlugins { get; } 
    private Lock DiscoveryLock { get; } = new(); // C# 13 的新特性，用于线程安全

    /// <summary>
    /// 初始化插件发现服务。
    /// </summary>
    public DefaultPluginDiscoveryService(string pluginsRootPath)
    {
        // 使用延迟加载和锁来确保扫描只执行一次
        lock (this.DiscoveryLock)
        {
            if (this.DiscoveredPlugins is not null)
                return;
            
            var plugins = new List<DiscoveredPlugin>();

            if (Directory.Exists(pluginsRootPath))
            {
                plugins.AddRange(Directory.GetDirectories(pluginsRootPath)
                    .Select(pluginDir => new { pluginDir, pluginDirInfo = new DirectoryInfo(pluginDir) })
                    .Select(it => new { self = it })
                    .Select(it => new DiscoveredPlugin
                    {
                        Name = it.self.pluginDirInfo.Name,
                        PhysicalPath = it.self.pluginDir,
                        DllPaths = Directory.GetFiles(it.self.pluginDir, "*.dll", SearchOption.TopDirectoryOnly),
                    }));
            }

            this.DiscoveredPlugins = new ReadOnlyCollection<DiscoveredPlugin>(plugins);
            Console.WriteLine($"发现 {this.DiscoveredPlugins.Count} 个插件。");
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<DiscoveredPlugin> DiscoverPlugins() => this.DiscoveredPlugins;
}