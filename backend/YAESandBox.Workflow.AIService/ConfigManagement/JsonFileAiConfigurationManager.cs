// 文件: JsonFileAiConfigurationManager.cs

using FluentResults;
using YAESandBox.Depend.Results;
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
    private IGeneralJsonStorage GeneralJsonStorage { get; } = generalJsonStorage;


    /// <summary>
    /// 从文件异步加载配置列表。这是获取最新数据的权威来源。
    /// </summary>
    private async Task<Result<Dictionary<string, AiConfigurationSet>>> LoadConfigurationsFromFileAsync()
    {
        var result = await this.GeneralJsonStorage.ForConfig().LoadAllAsync<Dictionary<string, AiConfigurationSet>>(ConfigFileName);
        if (!result.TryGetValue(out var value))
            return result.ToResult();

        if (value == null || value.Count == 0)
            return AiConfigurationSet.MakeDefaultDictionary();
        return value;
    }

    /// <summary>
    /// 将配置列表异步保存到文件。
    /// 保存完毕后会刷新缓存。
    /// </summary>
    private async Task<Result> SaveConfigurationsToFileAsync(Dictionary<string, AiConfigurationSet> configs)
    {
        return await this.GeneralJsonStorage.ForConfig().SaveAllAsync(configs, ConfigFileName);
    }

    /// <summary>
    /// 获取当前配置列表，优先从缓存读取，缓存未命中则从文件加载。
    /// </summary>
    private async Task<Result<IReadOnlyDictionary<string, AiConfigurationSet>>> GetCurrentConfigurationsAsync()
    {
        var loadedConfigs = await this.LoadConfigurationsFromFileAsync();
        if (!loadedConfigs.TryGetValue(out var value))
            return loadedConfigs.ToResult();
        return value;
    }

    /// <inheritdoc/>
    public async Task<Result<string>> AddConfigurationAsync(AiConfigurationSet config)
    {
        var configs = await this.LoadConfigurationsFromFileAsync();
        if (!configs.TryGetValue(out var value))
            return configs.ToResult();
        string newUuid = Guid.NewGuid().ToString();

        // 理论上 Guid.NewGuid() 碰撞概率极低，但严谨起见可以检查 (尽管对于 Guid 通常不这么做)
        // while (currentConfigs.ContainsKey(newUuid)) { newUuid = Guid.NewGuid().ToString(); }

        value[newUuid] = config;
        var result = await this.SaveConfigurationsToFileAsync(value);
        if (result.IsFailed)
            return result;
        return Result.Ok(newUuid);
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateConfigurationAsync(string uuid, AiConfigurationSet config)
    {
        if (string.IsNullOrWhiteSpace(uuid)) return NormalError.BadRequest("UUID 不能为空。");

        var configs = await this.LoadConfigurationsFromFileAsync();
        if (!configs.TryGetValue(out var value))
            return configs.ToResult();

        if (!value.ContainsKey(uuid))
            return NormalError.NotFound($"未找到 UUID 为 '{uuid}' 的配置，无法更新。");

        value[uuid] = config;

        return await this.SaveConfigurationsToFileAsync(value);
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteConfigurationAsync(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return NormalError.BadRequest("要删除的配置 UUID 不能为空。");

        var configs = await this.LoadConfigurationsFromFileAsync();
        if (!configs.TryGetValue(out var value))
            return configs.ToResult();

        if (!value.Remove(uuid))
            return Result.Ok(); //本来就没有，无需删除

        return await this.SaveConfigurationsToFileAsync(value);
    }

    /// <inheritdoc/>
    public async Task<Result<AiConfigurationSet>> GetConfigurationByUuidAsync(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return NormalError.BadRequest("要获取的配置 UUID 不能为空。");

        var configs = await this.GetCurrentConfigurationsAsync();

        if (!configs.TryGetValue(out var value))
            return configs.ToResult();
        if (value.TryGetValue(uuid, out var config))
            return Result.Ok(config);
        return NormalError.NotFound($"未找到 UUID 为 '{uuid}' 的配置。");
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyDictionary<string, AiConfigurationSet>>> GetAllConfigurationsAsync()
    {
        // GetCurrentConfigurationsAsync 总是返回列表的副本或新加载的列表
        return await this.GetCurrentConfigurationsAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 注意：此方法是同步的，它会阻塞等待异步操作完成。
    /// 在大部分情况下，都存在缓存，因而几乎不需要任何的等待。
    /// </remarks>
    AiConfigurationSet? IAiConfigurationProvider.GetConfigurationSet(string aiConfigKey)
    {
        if (string.IsNullOrWhiteSpace(aiConfigKey)) return null;

        // .GetAwaiter().GetResult() 用于在同步方法中调用异步方法并等待结果。
        // 这会阻塞当前线程，对于某些场景可能不是最佳选择，但符合接口定义和用户不强调异步复杂性的情况。
        var result = this.GetConfigurationByUuidAsync(aiConfigKey).GetAwaiter().GetResult();
        if (result.TryGetValue(out var value))
            return value;
        return null;
    }
}