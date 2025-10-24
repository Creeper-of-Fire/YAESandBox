using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Storage;
using YAESandBox.ModuleSystem.Abstractions.PluginDiscovery;
using YAESandBox.Workflow.Core.Config;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.Config.Stored;
using YAESandBox.Workflow.Core.Service;

namespace YAESandBox.Plugin.FileSystemConfigProvider;

/// <summary>
/// 一个从文件系统加载内部配置的插件。
/// 它会扫描插件DLL目录下的 "InnerConfigs" 文件夹，
/// 并加载 "Workflows", "Tuums", "Runes" 子目录中的所有 .json 配置文件。
/// </summary>
public class FileSystemConfigProviderPlugin : IYaeSandBoxPlugin, IProgramModuleInnerConfigProvider
{
    private static readonly IAppLogger Logger = AppLogging.CreateLogger<FileSystemConfigProviderPlugin>();

    private const string RootConfigDirectoryName = "InnerConfigs";
    private const string WorkflowSubdirectory = "Workflows";
    private const string TuumSubdirectory = "Tuums";
    private const string RuneSubdirectory = "Runes";

    private string PluginBasePath { get; }

    /// <inheritdoc />
    public PluginMetadata Metadata { get; } = new(
        Id: "YAESandBox.Plugin.FileSystemConfigProvider",
        Name: "文件系统配置提供程序",
        Version: "1.0.0",
        Author: "Creeper_of_Fire",
        Description: "一个通过扫描本地文件目录来提供“官方”内置配置的插件。"
    );

    /// <inheritdoc cref="FileSystemConfigProviderPlugin"/>
    public FileSystemConfigProviderPlugin()
    {
        // 在构造函数中确定插件的根目录，确保路径在整个生命周期内都可用
        string assemblyLocation = typeof(FileSystemConfigProviderPlugin).Assembly.Location;
        this.PluginBasePath = Path.GetDirectoryName(assemblyLocation) ?? string.Empty;

        if (string.IsNullOrEmpty(this.PluginBasePath))
        {
            Logger.Error("无法确定插件的基准目录，FileSystemConfigProviderPlugin 将无法加载任何配置。");
        }
    }

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service) { }

    /// <inheritdoc />
    public IReadOnlyList<StoredConfig<WorkflowConfig>> GetWorkflowInnerConfigs()
    {
        return this.LoadConfigsFromDirectory<WorkflowConfig>(WorkflowSubdirectory);
    }

    /// <inheritdoc />
    public IReadOnlyList<StoredConfig<TuumConfig>> GetTuumInnerConfigs()
    {
        return this.LoadConfigsFromDirectory<TuumConfig>(TuumSubdirectory);
    }

    /// <inheritdoc />
    public IReadOnlyList<StoredConfig<AbstractRuneConfig>> GetRuneInnerConfigs()
    {
        return this.LoadConfigsFromDirectory<AbstractRuneConfig>(RuneSubdirectory);
    }

    /// <summary>
    /// 从指定的子目录中加载所有 .json 配置文件并反序列化。
    /// </summary>
    /// <typeparam name="TConfig">配置内容的类型。</typeparam>
    /// <param name="subdirectoryName">要扫描的子目录名（例如 "Workflows"）。</param>
    /// <returns>一个包含已加载配置的只读列表。</returns>
    private List<StoredConfig<TConfig>> LoadConfigsFromDirectory<TConfig>(string subdirectoryName)
        where TConfig : IConfigStored
    {
        if (string.IsNullOrEmpty(this.PluginBasePath))
        {
            return [];
        }

        string targetDirectory = Path.Combine(this.PluginBasePath, RootConfigDirectoryName, subdirectoryName);

        if (!Directory.Exists(targetDirectory))
        {
            // 目录不存在是正常情况，表示该插件没有提供这类配置，无需记录为错误。
            Logger.Debug("配置目录不存在，跳过加载: {DirectoryPath}", targetDirectory);
            return [];
        }

        var loadedConfigs = new List<StoredConfig<TConfig>>();
        var jsonFiles = Directory.EnumerateFiles(targetDirectory, "*.json", SearchOption.AllDirectories);

        foreach (string filePath in jsonFiles)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath);

                // 使用项目中的JSON辅助类进行反序列化，确保配置一致
                var storedConfig = YaeSandBoxJsonHelper.Deserialize<StoredConfig<TConfig>>(jsonContent);

                if (storedConfig != null)
                {
                    loadedConfigs.Add(storedConfig);
                    Logger.Info("成功从文件加载内部配置: [{Type}] {Name} (来自: {FilePath})",
                        typeof(TConfig).Name, storedConfig.Name, Path.GetFileName(filePath));
                }
                else
                {
                    Logger.Warn("文件 '{FilePath}' 反序列化结果为空，已跳过。", filePath);
                }
            }
            catch (JsonException jsonEx)
            {
                Logger.Error(jsonEx, "解析配置文件 '{FilePath}' 时发生JSON错误，已跳过。", filePath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "加载配置文件 '{FilePath}' 时发生未知错误，已跳过。", filePath);
            }
        }

        return loadedConfigs;
    }
}