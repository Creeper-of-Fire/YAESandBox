// 文件: JsonFileAiConfigurationManager.cs

using FluentResults;
using System.Text.Json;
using YAESandBox.Depend;

// 为了使用 AbstractAiProcessorConfig 和 AiError

namespace YAESandBox.Workflow.AIService.ConfigManagement;

/// <summary>
/// 使用 JSON 文件持久化 AI 配置的管理器。
/// </summary>
public class JsonFileAiConfigurationManager(string? configFilePath = null) : IAiConfigurationManager
{
    private const string DefaultConfigFileName = "ai_configurations.json"; // 默认配置文件名
    private const string DefaultConfigDirectoryName = "AiConfigs"; // 默认配置存储目录名 (位于程序运行目录下)
    private static readonly JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };

    private string filePath { get; } = string.IsNullOrWhiteSpace(configFilePath)
        ? Path.Combine(Path.Combine(AppContext.BaseDirectory, DefaultConfigDirectoryName), DefaultConfigFileName)
        : configFilePath;

    private SemaphoreSlim fileLock { get; } = new(initialCount: 1, maxCount: 1);

    // 简单的内存缓存，用于存储从文件加载的配置列表
    // 注意：此缓存不会自动检测外部对JSON文件的修改。
    private IReadOnlyDictionary<string, AbstractAiProcessorConfig>? cachedConfigurations { get; set; }
    private Lock cacheLock { get; } = new(); // 用于保护 cachedConfigurations 的线程安全访问


    /// <summary>
    /// 从文件异步加载配置列表。这是获取最新数据的权威来源。
    /// </summary>
    private async Task<Result<Dictionary<string, AbstractAiProcessorConfig>>> LoadConfigurationsFromFileAsync()
    {
        await this.fileLock.WaitAsync(); // 获取文件锁
        try
        {
            string? directory = Path.GetDirectoryName(this.filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory); // 如果目录不存在，则创建它
            }

            if (!File.Exists(this.filePath))
                return AIConfigError.Error($"文件{this.filePath}不存在");

            string json = await File.ReadAllTextAsync(this.filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return AIConfigError.Error($"文件{this.filePath}为空");
            }

            var configs = JsonSerializer.Deserialize<Dictionary<string, AbstractAiProcessorConfig>>(json, jsonSerializerOptions);
            if (configs == null)
                return AIConfigError.Error($"反序列化 AI 配置时出错 ({this.filePath}) 的序列化结果为 null");
            return configs;
        }
        catch (JsonException ex)
        {
            return AIConfigError.Error($"反序列化 AI 配置时出错 ({this.filePath}): {ex.Message}");
        }
        catch (Exception ex) // 捕获其他文件操作等潜在错误
        {
            return AIConfigError.Error($"加载 AI 配置时出错 ({this.filePath}): {ex.Message}");
        }
        finally
        {
            this.fileLock.Release(); // 释放文件锁
        }
    }

    /// <summary>
    /// 将配置列表异步保存到文件。
    /// 保存完毕后会刷新缓存。
    /// </summary>
    private async Task<Result> SaveConfigurationsToFileAsync(Dictionary<string, AbstractAiProcessorConfig> configs)
    {
        string json = JsonSerializer.Serialize(configs, jsonSerializerOptions);
        await this.fileLock.WaitAsync(); // 获取文件锁
        try
        {
            string? directory = Path.GetDirectoryName(this.filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) 
                Directory.CreateDirectory(directory); // 确保目录存在

            await File.WriteAllTextAsync(this.filePath, json);

            // 文件保存成功后，更新内存缓存
            lock (this.cacheLock)
            {
                this.cachedConfigurations = new Dictionary<string, AbstractAiProcessorConfig>(configs); // 创建副本存入缓存
            }
        }
        catch (Exception ex) // 捕获文件写入等潜在错误
        {
            return AIConfigError.Error($"保存 AI 配置时出错 ({this.filePath}): {ex.Message}");
        }

        this.fileLock.Release(); // 释放文件锁
        return Result.Ok();
    }

    /// <summary>
    /// 获取当前配置列表，优先从缓存读取，缓存未命中则从文件加载。
    /// </summary>
    private async Task<Result<IReadOnlyDictionary<string, AbstractAiProcessorConfig>>> GetCurrentConfigurationsAsync()
    {
        IReadOnlyDictionary<string, AbstractAiProcessorConfig>? configsFromCache;
        lock (this.cacheLock)
        {
            configsFromCache = this.cachedConfigurations;
        }

        if (configsFromCache != null)
            return new Dictionary<string, AbstractAiProcessorConfig>(configsFromCache); // 返回缓存的副本

        var loadedConfigs = await this.LoadConfigurationsFromFileAsync();
        if (loadedConfigs.IsFailed) return loadedConfigs.ToResult();
        lock (this.cacheLock)
        {
            // 双重检查，防止在等待 LoadAsync 时其他线程已填充缓存
            this.cachedConfigurations ??= new Dictionary<string, AbstractAiProcessorConfig>(loadedConfigs.Value);
        }

        return loadedConfigs.Value; // LoadAsync 已经返回了新列表或副本
    }

    /// <inheritdoc/>
    public async Task<Result<string>> AddConfigurationAsync(AbstractAiProcessorConfig config)
    {
        var configs = await this.LoadConfigurationsFromFileAsync();
        if (configs.IsFailed) return configs.ToResult();
        string newUuid = Guid.NewGuid().ToString();

        // 理论上 Guid.NewGuid() 碰撞概率极低，但严谨起见可以检查 (尽管对于 Guid 通常不这么做)
        // while (currentConfigs.ContainsKey(newUuid)) { newUuid = Guid.NewGuid().ToString(); }

        configs.Value[newUuid] = config;
        await this.SaveConfigurationsToFileAsync(configs.Value);
        return Result.Ok();
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateConfigurationAsync(string uuid, AbstractAiProcessorConfig config)
    {
        if (string.IsNullOrWhiteSpace(uuid)) return Result.Fail("UUID 不能为空。");

        var configs = await this.LoadConfigurationsFromFileAsync();
        if (configs.IsFailed)
            return configs.ToResult();

        if (!configs.Value.ContainsKey(uuid))
            return Result.Fail(AiError.Error($"未找到 UUID 为 '{uuid}' 的配置，无法更新。"));

        configs.Value[uuid] = config;

        await this.SaveConfigurationsToFileAsync(configs.Value);
        return Result.Ok();
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteConfigurationAsync(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return Result.Fail(AiError.Error("要删除的配置 UUID 不能为空。"));

        var configs = await this.LoadConfigurationsFromFileAsync();
        if (configs.IsFailed)
            return configs.ToResult();

        if (!configs.Value.Remove(uuid))
            return Result.Ok(); //本来就没有，无需删除

        await this.SaveConfigurationsToFileAsync(configs.Value);
        return Result.Ok();
    }

    /// <inheritdoc/>
    public async Task<Result<AbstractAiProcessorConfig>> GetConfigurationByUuidAsync(string uuid)
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
    public async Task<Result<IReadOnlyDictionary<string, AbstractAiProcessorConfig>>> GetAllConfigurationsAsync()
    {
        var configs = await this.GetCurrentConfigurationsAsync();
        // GetCurrentConfigurationsAsync 总是返回列表的副本或新加载的列表
        return configs;
    }

    /// <summary>
    /// 实现 IAiConfigurationProvider 的 GetConfiguration 方法。
    /// 注意：此方法是同步的，它会阻塞等待异步操作完成。
    /// 在大部分情况下，都存在缓存，因而几乎不需要任何的等待。
    /// </summary>
    AbstractAiProcessorConfig? IAiConfigurationProvider.GetConfiguration(string aiConfigKey)
    {
        if (string.IsNullOrWhiteSpace(aiConfigKey)) return null;

        // .GetAwaiter().GetResult() 用于在同步方法中调用异步方法并等待结果。
        // 这会阻塞当前线程，对于某些场景可能不是最佳选择，但符合接口定义和用户不强调异步复杂性的情况。
        var result = this.GetConfigurationByUuidAsync(aiConfigKey).GetAwaiter().GetResult();
        return result.IsSuccess ? result.Value : null;
    }
}