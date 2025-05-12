// 文件: JsonFileAiConfigurationManager.cs

using FluentResults;
using Nito.AsyncEx;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService.AiConfig;

// 为了使用 AbstractAiProcessorConfig 和 AiError

namespace YAESandBox.Workflow.AIService.ConfigManagement;

/// <summary>
/// 使用 JSON 文件持久化 AI 配置的管理器。
/// </summary>
public class JsonFileAiConfigurationManager(IGeneralJsonStorage generalJsonStorage) : IAiConfigurationManager
{
    private const string ConfigFileName = "ai_configurations.json"; // 默认配置文件名
    private const string ConfigDirectory = "Configurations"; // 默认配置存储目录名 (位于程序运行目录下)
    private IGeneralJsonStorage generalJsonStorage { get; } = generalJsonStorage;

    private AsyncLock fileLock { get; } = new();

    // 简单的内存缓存，用于存储从文件加载的配置列表
    // 注意：此缓存不会自动检测外部对JSON文件的修改。
    private IReadOnlyDictionary<string, AiConfigurationSet>? cachedConfigurations { get; set; }
    private Lock cacheLock { get; } = new(); // 用于保护 cachedConfigurations 的线程安全访问


    /// <summary>
    /// 从文件异步加载配置列表。这是获取最新数据的权威来源。
    /// </summary>
    private async Task<Result<Dictionary<string, AiConfigurationSet>>> LoadConfigurationsFromFileAsync()
    {
        var result = await this.generalJsonStorage.LoadAllAsync<Dictionary<string, AiConfigurationSet>>(ConfigDirectory, ConfigFileName);
        if (result.IsFailed)
            return result.ToResult();

        var sets = result.Value;
        if (sets == null || sets.Count == 0)
            return AiConfigurationSet.MakeDefaultDictionary();
        return sets;
    }

    /// <summary>
    /// 将配置列表异步保存到文件。
    /// 保存完毕后会刷新缓存。
    /// </summary>
    private async Task<Result> SaveConfigurationsToFileAsync(Dictionary<string, AiConfigurationSet> configs)
    {
        var result = await this.generalJsonStorage.SaveAllAsync(ConfigDirectory, ConfigFileName, configs);
        if (result.IsFailed)
            return result;

        // 文件保存成功后，更新内存缓存
        lock (this.cacheLock)
        {
            this.cachedConfigurations = new Dictionary<string, AiConfigurationSet>(configs); // 创建副本存入缓存
        }

        return Result.Ok();
    }

    /// <summary>
    /// 获取当前配置列表，优先从缓存读取，缓存未命中则从文件加载。
    /// </summary>
    private async Task<Result<IReadOnlyDictionary<string, AiConfigurationSet>>> GetCurrentConfigurationsAsync()
    {
        IReadOnlyDictionary<string, AiConfigurationSet>? configsFromCache;
        lock (this.cacheLock)
        {
            configsFromCache = this.cachedConfigurations;
        }

        if (configsFromCache != null)
            return new Dictionary<string, AiConfigurationSet>(configsFromCache); // 返回缓存的副本

        var loadedConfigs = await this.LoadConfigurationsFromFileAsync();
        if (loadedConfigs.IsFailed) return loadedConfigs.ToResult();
        lock (this.cacheLock)
        {
            // 双重检查，防止在等待 LoadAsync 时其他线程已填充缓存
            this.cachedConfigurations ??= new Dictionary<string, AiConfigurationSet>(loadedConfigs.Value);
        }

        return loadedConfigs.Value; // LoadAsync 已经返回了新列表或副本
    }

    /// <inheritdoc/>
    public async Task<Result<string>> AddConfigurationAsync(AiConfigurationSet config)
    {
        var configs = await this.LoadConfigurationsFromFileAsync();
        if (configs.IsFailed)
            return configs.ToResult();
        string newUuid = Guid.NewGuid().ToString();

        // 理论上 Guid.NewGuid() 碰撞概率极低，但严谨起见可以检查 (尽管对于 Guid 通常不这么做)
        // while (currentConfigs.ContainsKey(newUuid)) { newUuid = Guid.NewGuid().ToString(); }

        configs.Value[newUuid] = config;
        var result = await this.SaveConfigurationsToFileAsync(configs.Value);
        if (result.IsFailed)
            return result;
        return Result.Ok(newUuid);
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateConfigurationAsync(string uuid, AiConfigurationSet config)
    {
        if (string.IsNullOrWhiteSpace(uuid)) return Result.Fail("UUID 不能为空。");

        var configs = await this.LoadConfigurationsFromFileAsync();
        if (configs.IsFailed)
            return configs.ToResult();

        if (!configs.Value.ContainsKey(uuid))
            return AiError.Error($"未找到 UUID 为 '{uuid}' 的配置，无法更新。");

        configs.Value[uuid] = config;

        var result = await this.SaveConfigurationsToFileAsync(configs.Value);
        if (result.IsFailed)
            return result;
        return Result.Ok();
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteConfigurationAsync(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return AiError.Error("要删除的配置 UUID 不能为空。");

        var configs = await this.LoadConfigurationsFromFileAsync();
        if (configs.IsFailed)
            return configs.ToResult();

        if (!configs.Value.Remove(uuid))
            return Result.Ok(); //本来就没有，无需删除

        var result = await this.SaveConfigurationsToFileAsync(configs.Value);
        if (result.IsFailed)
            return result;
        return Result.Ok();
    }

    /// <inheritdoc/>
    public async Task<Result<AiConfigurationSet>> GetConfigurationByUuidAsync(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return AIConfigError.Error("要获取的配置 UUID 不能为空。");

        var configs = await this.GetCurrentConfigurationsAsync();
        if (configs.IsFailed)
            return configs.ToResult();

        return configs.Value.TryGetValue(uuid, out var config)
            ? Result.Ok(config)
            : AIConfigError.Error($"未找到 UUID 为 '{uuid}' 的配置。");
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyDictionary<string, AiConfigurationSet>>> GetAllConfigurationsAsync()
    {
        var configs = await this.GetCurrentConfigurationsAsync();
        // GetCurrentConfigurationsAsync 总是返回列表的副本或新加载的列表
        if (configs.IsFailed)
            return configs.ToResult();
        return configs;
    }

    /// <summary>
    /// 实现 IAiConfigurationProvider 的 GetConfigurationSet 方法。
    /// 注意：此方法是同步的，它会阻塞等待异步操作完成。
    /// 在大部分情况下，都存在缓存，因而几乎不需要任何的等待。
    /// </summary>
    AiConfigurationSet? IAiConfigurationProvider.GetConfigurationSet(string aiConfigKey)
    {
        if (string.IsNullOrWhiteSpace(aiConfigKey)) return null;

        // .GetAwaiter().GetResult() 用于在同步方法中调用异步方法并等待结果。
        // 这会阻塞当前线程，对于某些场景可能不是最佳选择，但符合接口定义和用户不强调异步复杂性的情况。
        var result = this.GetConfigurationByUuidAsync(aiConfigKey).GetAwaiter().GetResult();
        return result.IsSuccess ? result.Value : null;
    }
}