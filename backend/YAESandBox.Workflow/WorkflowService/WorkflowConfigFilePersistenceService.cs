using System.Text.Json;
using YAESandBox.Authentication.Storage;
using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.Config.Stored;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;

namespace YAESandBox.Workflow.WorkflowService;

/// <summary>
/// 工作流配置查询服务，负责查询用户在物理存储中的配置文件，并且提供查询、保存、删除功能。
/// </summary>
/// <param name="userStorageFactory"></param>
public class WorkflowConfigFilePersistenceService(IUserScopedStorageFactory userStorageFactory)
{
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger<WorkflowConfigFilePersistenceService>();
    private IUserScopedStorageFactory UserStorageFactory { get; } = userStorageFactory;
    private static string MakeFileName(string storeId) => $"{storeId}.json";
    private static string GetIdFromFileName(string fileName) => Path.GetFileNameWithoutExtension(fileName);
    private const string MainDirectory = "WorkflowConfigurations";
    private const string WorkflowDirectory = "GlobalWorkflows";
    private const string TuumDirectory = "GlobalTuums";
    private const string RuneDirectory = "GlobalRunes";

    private ScopeTemplate ForWorkflow { get; } =
        ConfigRoot().CreateScope(MainDirectory).CreateScope(WorkflowDirectory);

    private ScopeTemplate ForTuum { get; } =
        ConfigRoot().CreateScope(MainDirectory).CreateScope(TuumDirectory);

    private ScopeTemplate ForRune { get; } =
        ConfigRoot().CreateScope(MainDirectory).CreateScope(RuneDirectory);

    /// <summary>
    /// 只在全局的工作流配置中查找，不查找内联的私有部分（虽然对于工作流没有这部分，但是出于整齐的考虑还是这么写了）
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="storeId"></param>
    /// <returns></returns>
    internal async Task<Result<StoredConfig<WorkflowConfig>>> FindWorkflowConfig(string userId, string storeId) =>
        await this.FindConfig<WorkflowConfig>(this.ForWorkflow, userId, storeId);

    /// <summary>
    /// 只在全局的枢机配置中查找，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="storeId"></param>
    /// <returns></returns>
    internal async Task<Result<StoredConfig<TuumConfig>>> FindTuumConfig(string userId, string storeId) =>
        await this.FindConfig<TuumConfig>(this.ForTuum, userId, storeId);

    /// <summary>
    /// 只在全局的符文配置中查找，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="storeId"></param>
    /// <returns></returns>
    internal async Task<Result<StoredConfig<AbstractRuneConfig>>> FindRuneConfig(string userId, string storeId) =>
        await this.FindConfig<AbstractRuneConfig>(this.ForRune, userId, storeId);

    /// <summary>
    /// 只寻找所有的全局工作流配置，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, StoredConfig<WorkflowConfig>>> FindAllWorkflowConfig(string userId) =>
        await this.FindAllConfig<WorkflowConfig>(this.ForWorkflow, userId);

    /// <summary>
    /// 只寻找所有的全局枢机配置，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, StoredConfig<TuumConfig>>> FindAllTuumConfig(string userId) =>
        await this.FindAllConfig<TuumConfig>(this.ForTuum, userId);

    /// <summary>
    /// 只寻找所有的全局符文配置，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, StoredConfig<AbstractRuneConfig>>> FindAllRuneConfig(string userId) =>
        await this.FindAllConfig<AbstractRuneConfig>(this.ForRune, userId);

    /// <summary>
    /// 保存工作流配置到全局
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="storeId"></param>
    /// <param name="workflowConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveWorkflowConfig(string userId, string storeId, StoredConfig<WorkflowConfig> workflowConfig) =>
        await this.SaveConfig(this.ForWorkflow, userId, storeId, workflowConfig);

    /// <summary>
    /// 保存枢机配置到全局
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="storeId"></param>
    /// <param name="tuumConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveTuumConfig(string userId, string storeId, StoredConfig<TuumConfig> tuumConfig) =>
        await this.SaveConfig(this.ForTuum, userId, storeId, tuumConfig);

    /// <summary>
    /// 保存符文配置到全局
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="storeId"></param>
    /// <param name="abstractRuneConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveRuneConfig(string userId, string storeId, StoredConfig<AbstractRuneConfig> abstractRuneConfig) =>
        await this.SaveConfig(this.ForRune, userId, storeId, abstractRuneConfig);

    /// <summary>
    /// 删除全局的工作流配置
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="storeId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteWorkflowConfig(string userId, string storeId) =>
        await this.DeleteConfig(this.ForWorkflow, userId, storeId);

    /// <summary>
    /// 删除全局的枢机配置
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="storeId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteTuumConfig(string userId, string storeId) =>
        await this.DeleteConfig(this.ForTuum, userId, storeId);

    /// <summary>
    /// 删除全局的符文配置
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="storeId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteRuneConfig(string userId, string storeId) =>
        await this.DeleteConfig(this.ForRune, userId, storeId);

    private async Task<Result<StoredConfig<T>>> FindConfig<T>(ScopeTemplate template, string userId, string storeId)
        where T : IConfigStored
    {
        // 从工厂获取用户专属的存储实例
        var userStorageResult = await this.UserStorageFactory.GetFinalStorageForUserAsync(userId, template);
        if (userStorageResult.TryGetError(out var userStorageError, out var userStorage))
            return userStorageError;

        string filePath = MakeFileName(storeId);

        // 使用用户存储实例进行操作
        var storedConfigResult = await userStorage.LoadAllAsync<StoredConfig<T>>(MakeFileName(storeId));
        if (storedConfigResult.TryGetValue(out var storedConfig, out var storedConfigError))
        {
            if (storedConfig is null)
                return NormalError.NotFound($"找不到指定的配置:{Path.Combine(userStorage.WorkPath, storeId)}.json");
            return storedConfig;
        }

        // 如果发生Json异常，通常是因为这是旧模型，则尝试按旧模型进行加载
        if (storedConfigError.Exception is not JsonException)
            return storedConfigError;

        Logger.Warn("按新模型 StoredConfig<{TypeName}> 加载配置 '{StoreId}' 失败，尝试按旧模型进行回退加载。", typeof(T).Name, storeId);

        var oldModelResult = await userStorage.LoadAllAsync<T>(filePath);
        if (oldModelResult.TryGetError(out var oldModelError, out var oldContent))
            return oldModelError;

        if (oldContent is null)
            return NormalError.NotFound($"找不到指定的配置:{Path.Combine(userStorage.WorkPath, storeId)}.json");

        Logger.Info("成功回退加载旧版本配置 '{StoreId}'。已在内存中将其升级为新版 StoredConfig 格式。", storeId);
        var upgradedConfig = new StoredConfig<T>
        {
            Content = oldContent,
            IsReadOnly = false, // 用户数据默认为可写
            Meta = new StoredConfigMeta
            {
                CreatedAt = DateTime.UtcNow, // 无法知道原始创建时间
                UpdatedAt = DateTime.UtcNow,
                Description = "此配置由旧版本格式自动迁移而来。"
            }
        };

        // 尝试将升级后的新格式写回文件系统，完成一次迁移。
        _ = userStorage.SaveAllAsync(upgradedConfig, filePath);

        return Result.Ok(upgradedConfig);

        // 辅助方法，尝试通过反射从内容对象中获取 Name 属性
        static string? TryGetNameFromContent(T content)
        {
            var nameProperty =
                typeof(T).GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return nameProperty?.GetValue(content) as string;
        }
    }

    private async Task<DictionaryResult<string, StoredConfig<T>>> FindAllConfig<T>(ScopeTemplate template, string userId)
        where T : IConfigStored
    {
        var userStorageResult = await this.UserStorageFactory.GetFinalStorageForUserAsync(userId, template);
        if (userStorageResult.TryGetError(out var error, out var finalStorage))
            return DictionaryResult<string, StoredConfig<T>>.Fail(error);

        var fileListResult = await finalStorage.ListFileNamesAsync();
        if (fileListResult.TryGetError(out error, out var allFileNames))
            return DictionaryResult<string, StoredConfig<T>>.Fail(error);

        // 1. 为每个文件名创建一个加载任务。
        //    这个任务会异步执行 FindConfig，并返回一个包含 ID 和结果的元组。
        var loadTasks = allFileNames.Select(async fileName =>
        {
            string storeId = GetIdFromFileName(fileName);
            // 注意：这里我们不再直接 await FindConfig，而是让它返回一个 Task<Result<T>>
            var configResult = await this.FindConfig<T>(template, userId, storeId);
            return (StoreId: storeId, Result: configResult);
        }).ToList();

        // 2. 使用 Task.WhenAll 并行执行所有加载任务。
        //    这将返回一个包含所有结果元组的数组。
        var resultsArray = await Task.WhenAll(loadTasks);

        // 3. 将结果数组转换为字典。
        var configResults = resultsArray.ToDictionary(
            keySelector: tuple => tuple.StoreId,
            elementSelector: tuple => tuple.Result
        );

        return configResults;
    }

    private async Task<Result> DeleteConfig(ScopeTemplate template, string userId, string storeId)
    {
        var userStorageResult = await this.UserStorageFactory.GetFinalStorageForUserAsync(userId, template);
        if (userStorageResult.TryGetError(out var error, out var finalStorage))
            return error;

        return await finalStorage.DeleteFileAsync(MakeFileName(storeId));
    }

    private async Task<Result> SaveConfig<T>(ScopeTemplate template, string userId, string storeId, StoredConfig<T> config)
        where T : IConfigStored
    {
        if (config.IsReadOnly)
        {
            return Result.Fail(NormalError.ValidationError(
                "操作被拒绝：无法修改或保存一个只读配置。" +
                "尽管如此，如果您确认这是个存在于真实存储系统中的配置，它的只读性很可能是由于故障导致，您可以使用删除方法来删除它。"
            ));
        }

        var userStorageResult = await this.UserStorageFactory.GetFinalStorageForUserAsync(userId, template);
        if (userStorageResult.TryGetError(out var error, out var finalStorage))
            return error;

        return await finalStorage.SaveAllAsync(config, MakeFileName(storeId));
    }
}