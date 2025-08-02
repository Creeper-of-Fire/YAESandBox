// 文件: JsonFileAiConfigurationManager.cs

using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService.AiConfig;

namespace YAESandBox.Workflow.AIService.ConfigManagement;

/// <summary>
/// 使用 JSON 文件持久化 AI 配置的管理器。
/// 现在将每个用户的配置存储在其ID命名的子目录中，以实现多租户。
/// </summary>
public class JsonFileAiConfigurationManager(IGeneralJsonStorage generalJsonStorage) : IAiConfigurationManager
{
    private const string ConfigFileName = "ai_configurations.json"; // 默认配置文件名
    private ScopedStorageFactory.ScopedJsonStorage ScopedJsonStorage { get; } = generalJsonStorage.ForConfig();
    
    /// <summary>
    /// 从指定用户的文件异步加载配置列表。
    /// </summary>
    private async Task<Result<Dictionary<string, AiConfigurationSet>>> LoadConfigurationsFromFileAsync(string userId)
    {
        // 将 userId 作为子目录传递，实现用户数据隔离
        var result = await this.ScopedJsonStorage.LoadAllAsync<Dictionary<string, AiConfigurationSet>>(ConfigFileName, userId);
        if (result.TryGetError(out var error, out var value))
            return error;

        if (value == null || value.Count == 0)
        {
            // 如果用户是首次使用，为其创建默认配置
            return AiConfigurationSet.MakeDefaultDictionary();
        }
        return value;
    }
    
    /// <summary>
    /// 将配置列表异步保存到指定用户的文件。
    /// </summary>
    private async Task<Result> SaveConfigurationsToFileAsync(string userId, Dictionary<string, AiConfigurationSet> configs)
    {
        // 将 userId 作为子目录传递
        return await this.ScopedJsonStorage.SaveAllAsync(configs, ConfigFileName, userId);
    }

    /// <inheritdoc/>
    public async Task<Result<UpsertResultType>> UpsertConfigurationAsync(string userId, string uuid, AiConfigurationSet config)
    {
        if (string.IsNullOrWhiteSpace(userId)) return NormalError.BadRequest("用户ID不能为空。");
        if (string.IsNullOrWhiteSpace(uuid)) return NormalError.BadRequest("配置UUID不能为空。");

        var loadResult = await this.LoadConfigurationsFromFileAsync(userId);
        if (loadResult.TryGetError(out var error, out var configs))
            return error;

        bool wasPresent = configs.ContainsKey(uuid);
        configs[uuid] = config;

        var saveResult = await this.SaveConfigurationsToFileAsync(userId, configs);
        if (saveResult.TryGetError(out var error1))
            return error1;

        return Result.Ok(wasPresent ? UpsertResultType.Updated : UpsertResultType.Created);
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteConfigurationAsync(string userId, string uuid)
    {
        if (string.IsNullOrWhiteSpace(userId)) return NormalError.BadRequest("用户ID不能为空。");
        if (string.IsNullOrWhiteSpace(uuid)) return NormalError.BadRequest("要删除的配置UUID不能为空。");

        var configsResult = await this.LoadConfigurationsFromFileAsync(userId);
        if (configsResult.TryGetError(out var error, out var configs))
            return error;

        if (!configs.Remove(uuid))
            return Result.Ok(); // 本来就没有，无需删除，操作成功

        return await this.SaveConfigurationsToFileAsync(userId, configs);
    }
    
    /// <inheritdoc/>
    public async Task<Result<AiConfigurationSet>> GetConfigurationByUuidAsync(string userId, string uuid)
    {
        if (string.IsNullOrWhiteSpace(userId)) return NormalError.BadRequest("用户ID不能为空。");
        if (string.IsNullOrWhiteSpace(uuid)) return NormalError.BadRequest("要获取的配置UUID不能为空。");

        var configsResult = await this.GetAllConfigurationsAsync(userId);

        if (configsResult.TryGetError(out var error, out var configs))
            return error;
        
        if (configs.TryGetValue(uuid, out var config))
            return Result.Ok(config);
        
        return NormalError.NotFound($"未找到UUID为 '{uuid}' 的配置。");
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyDictionary<string, AiConfigurationSet>>> GetAllConfigurationsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return NormalError.BadRequest("用户ID不能为空。");
        
        var loadedConfigs = await this.LoadConfigurationsFromFileAsync(userId);
        if (loadedConfigs.TryGetError(out var error, out var value))
            return error;

        // 返回一个 IReadOnlyDictionary 以防止外部修改
        return Result.Ok<IReadOnlyDictionary<string, AiConfigurationSet>>(value);
    }
    
    /// <inheritdoc />
    AiConfigurationSet? IAiConfigurationProvider.GetConfigurationSet(string userId, string aiConfigKey)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(aiConfigKey)) return null;
        
        // .GetAwaiter().GetResult() 用于在同步方法中调用异步方法并等待结果。
        var result = this.GetConfigurationByUuidAsync(userId, aiConfigKey).GetAwaiter().GetResult();
        if (result.TryGetValue(out var value))
            return value;
        return null;
    }
}