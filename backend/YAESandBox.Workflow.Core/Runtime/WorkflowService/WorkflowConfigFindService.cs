using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Core.Config;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.Config.Stored;
using static YAESandBox.Workflow.Core.Service.InnerConfigProviderResolver;

namespace YAESandBox.Workflow.Core.Runtime.WorkflowService;

/// <summary>
/// 工作流配置查询服务，可以查找内置配置和用户存储中的配置。
/// </summary>
/// <param name="workflowConfigFilePersistenceService"></param>
public class WorkflowConfigFindService(WorkflowConfigFilePersistenceService workflowConfigFilePersistenceService)
{
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger<WorkflowConfigFindService>();
    private WorkflowConfigFilePersistenceService WorkflowConfigFilePersistenceService { get; } = workflowConfigFilePersistenceService;

    /// <summary>
    /// 在用户存储和内置存储中寻找指定的全局工作流配置。
    /// </summary>
    public async Task<Result<StoredConfig<WorkflowConfig>>> FindWorkflowConfig(string userId, string storeId) =>
        await this.FindConfigInternalAsync(userId, storeId, WorkflowInnerConfigs,
            this.WorkflowConfigFilePersistenceService.FindWorkflowConfig);

    /// <summary>
    /// 在用户存储和内置存储中寻找指定的全局枢机配置。
    /// </summary>
    public async Task<Result<StoredConfig<TuumConfig>>> FindTuumConfig(string userId, string storeId) =>
        await this.FindConfigInternalAsync(userId, storeId, TuumInnerConfigs, this.WorkflowConfigFilePersistenceService.FindTuumConfig);

    /// <summary>
    /// 在用户存储和内置存储中寻找指定的全局符文配置。
    /// </summary>
    public async Task<Result<StoredConfig<AbstractRuneConfig>>> FindRuneConfig(string userId, string storeId) =>
        await this.FindConfigInternalAsync(userId, storeId, RuneInnerConfigs, this.WorkflowConfigFilePersistenceService.FindRuneConfig);

    /// <summary>
    /// 寻找所有的全局工作流配置
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, StoredConfig<WorkflowConfig>>> FindAllWorkflowConfig(string userId) =>
        await this.FindAllConfigInternalAsync(userId, WorkflowInnerConfigs,
            this.WorkflowConfigFilePersistenceService.FindAllWorkflowConfig);

    /// <summary>
    /// 寻找所有的全局枢机配置
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, StoredConfig<TuumConfig>>> FindAllTuumConfig(string userId) =>
        await this.FindAllConfigInternalAsync(userId, TuumInnerConfigs, this.WorkflowConfigFilePersistenceService.FindAllTuumConfig);

    /// <summary>
    /// 寻找所有的全局符文配置
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, StoredConfig<AbstractRuneConfig>>> FindAllRuneConfig(string userId) =>
        await this.FindAllConfigInternalAsync(userId, RuneInnerConfigs, this.WorkflowConfigFilePersistenceService.FindAllRuneConfig);

    /// <inheritdoc cref="WorkflowConfigFindService.SaveWorkflowConfig"/>
    public async Task<Result> SaveWorkflowConfig(string userId, string storeId, StoredConfig<WorkflowConfig> workflowConfig) =>
        await this.WorkflowConfigFilePersistenceService.SaveWorkflowConfig(userId, storeId, workflowConfig);

    /// <inheritdoc cref="WorkflowConfigFindService.SaveTuumConfig"/>
    public async Task<Result> SaveTuumConfig(string userId, string storeId, StoredConfig<TuumConfig> tuumConfig) =>
        await this.WorkflowConfigFilePersistenceService.SaveTuumConfig(userId, storeId, tuumConfig);

    /// <inheritdoc cref="WorkflowConfigFindService.SaveRuneConfig"/>
    public async Task<Result> SaveRuneConfig(string userId, string storeId, StoredConfig<AbstractRuneConfig> abstractRuneConfig) =>
        await this.WorkflowConfigFilePersistenceService.SaveRuneConfig(userId, storeId, abstractRuneConfig);

    /// <inheritdoc cref="WorkflowConfigFindService.DeleteWorkflowConfig"/>
    public async Task<Result> DeleteWorkflowConfig(string userId, string storeId) =>
        await this.WorkflowConfigFilePersistenceService.DeleteWorkflowConfig(userId, storeId);

    /// <inheritdoc cref="WorkflowConfigFindService.DeleteTuumConfig"/>
    public async Task<Result> DeleteTuumConfig(string userId, string storeId) =>
        await this.WorkflowConfigFilePersistenceService.DeleteTuumConfig(userId, storeId);

    /// <inheritdoc cref="WorkflowConfigFindService.DeleteRuneConfig"/>
    public async Task<Result> DeleteRuneConfig(string userId, string storeId) =>
        await this.WorkflowConfigFilePersistenceService.DeleteRuneConfig(userId, storeId);

    /// <summary>
    /// 根据 StoredConfigRef（RefId 和 Version）在用户存储和内置存储中寻找指定的全局符文配置。
    /// </summary>
    public async Task<Result<StoredConfig<AbstractRuneConfig>>> FindRuneConfigByRefAsync(string userId, StoredConfigRef targetRef)
    {
        // 1. 优先查找内置配置
        foreach (var innerConfig in RuneInnerConfigs.Values)
        {
            if (innerConfig.StoreRef is not null &&
                innerConfig.StoreRef.RefId == targetRef.RefId &&
                innerConfig.StoreRef.Version == targetRef.Version)
            {
                return Result.Ok(innerConfig);
            }
        }

        // 2. 如果不是内置配置，则查找用户存储
        return await this.WorkflowConfigFilePersistenceService.FindRuneConfigByRefAsync(userId, targetRef);
    }

    /// <summary>
    /// 通用的“按ID查找”逻辑。
    /// </summary>
    /// <param name="userId">用户ID。</param>
    /// <param name="storeId">要查找的存储ID。</param>
    /// <param name="innerConfigs">要查找的内部配置。</param>
    /// <param name="findInUserStorageAsync">用于在用户存储中查找的回调函数。</param>
    private async Task<Result<StoredConfig<T>>> FindConfigInternalAsync<T>(
        string userId,
        string storeId,
        IReadOnlyDictionary<string, StoredConfig<T>> innerConfigs,
        Func<string, string, Task<Result<StoredConfig<T>>>> findInUserStorageAsync)
        where T : IConfigStored
    {
        // 1. 优先查找内置配置
        if (innerConfigs.TryGetValue(storeId, out var config))
        {
            return Result.Ok(config);
        }

        // 2. 如果不是内置配置，则查找用户存储
        return await findInUserStorageAsync(userId, storeId);
    }

    /// <summary>
    /// 通用的“查找全部”逻辑。
    /// </summary>
    /// <param name="userId">用户ID。</param>
    /// <param name="innerConfigs">要查找的内部配置。</param>
    /// <param name="findAllInUserStorageAsync">用于在用户存储中查找的回调函数。</param>
    private async Task<DictionaryResult<string, StoredConfig<T>>> FindAllConfigInternalAsync<T>(
        string userId,
        IReadOnlyDictionary<string, StoredConfig<T>> innerConfigs,
        Func<string, Task<DictionaryResult<string, StoredConfig<T>>>> findAllInUserStorageAsync)
        where T : IConfigStored
    {
        var userConfigsResult = await findAllInUserStorageAsync(userId);

        if (userConfigsResult.IsFailed)
        {
            return userConfigsResult;
        }

        var innerConfigsResults = innerConfigs.ToDictionary(
            kvp => kvp.Key,
            kvp => Result.Ok(kvp.Value)
        );

        return userConfigsResult.MergeWith(innerConfigsResults);
    }
}