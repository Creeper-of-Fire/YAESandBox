using YAESandBox.Authentication.Storage;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Rune.Config;
using YAESandBox.Workflow.Tuum;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;

namespace YAESandBox.Workflow.Core;

/// <summary>
/// 工作流配置服务，主要承担全局模板的管理与访问。
/// 注意：此服务不参与运行时的配置解析。工作流执行时依赖的是前端提供的、包含所有内联子配置的完整配置快照。
///
/// 全局模板（如符文模板、枢机模板、完整工作流模板）提供可复用的配置蓝图。
/// 从模板拷贝时，其内容会被复制到具体的配置中，并为所有新生成的配置实例（包括配置自身及其内嵌子配置）分配全新的唯一ID。
///
/// 工作流的模板有所不同，它在前端会区分“运行”和“拷贝”。在运行时，它并不会重新分配子配置的ID，因为它自身会被分配一个ID用以区分。
///
/// 不论什么情况，后端都不会对ID进行任何的修改，那是前端的工作。
/// </summary>
/// <param name="userStorageFactory"></param>
public class WorkflowConfigFileService(IUserScopedStorageFactory userStorageFactory)
{
    private IUserScopedStorageFactory UserStorageFactory { get; } = userStorageFactory;
    private static string MakeFileName(string id) => $"{id}.json";
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
    /// <param name="workflowId"></param>
    /// <returns></returns>
    internal async Task<Result<WorkflowConfig>> FindWorkflowConfig(string userId, string workflowId) =>
        await this.FindConfig<WorkflowConfig>(this.ForWorkflow, userId, workflowId);

    /// <summary>
    /// 只在全局的枢机配置中查找，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="tuumId"></param>
    /// <returns></returns>
    internal async Task<Result<TuumConfig>> FindTuumConfig(string userId, string tuumId) =>
        await this.FindConfig<TuumConfig>(this.ForTuum, userId, tuumId);

    /// <summary>
    /// 只在全局的符文配置中查找，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="runeId"></param>
    /// <returns></returns>
    internal async Task<Result<AbstractRuneConfig>> FindRuneConfig(string userId, string runeId) =>
        await this.FindConfig<AbstractRuneConfig>(this.ForRune, userId, runeId);

    /// <summary>
    /// 只寻找所有的全局工作流配置，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, WorkflowConfig>> FindAllWorkflowConfig(string userId) =>
        await this.FindAllConfig<WorkflowConfig>(this.ForWorkflow, userId);

    /// <summary>
    /// 只寻找所有的全局枢机配置，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, TuumConfig>> FindAllTuumConfig(string userId) =>
        await this.FindAllConfig<TuumConfig>(this.ForTuum, userId);

    /// <summary>
    /// 只寻找所有的全局符文配置，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, AbstractRuneConfig>> FindAllRuneConfig(string userId) =>
        await this.FindAllConfig<AbstractRuneConfig>(this.ForRune, userId);

    /// <summary>
    /// 保存工作流配置到全局
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="workflowId"></param>
    /// <param name="workflowConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveWorkflowConfig(string userId, string workflowId, WorkflowConfig workflowConfig) =>
        await this.SaveConfig(this.ForWorkflow, userId, workflowId, workflowConfig);

    /// <summary>
    /// 保存枢机配置到全局
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="tuumId"></param>
    /// <param name="tuumConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveTuumConfig(string userId, string tuumId, TuumConfig tuumConfig) =>
        await this.SaveConfig(this.ForTuum, userId, tuumId, tuumConfig);

    /// <summary>
    /// 保存符文配置到全局
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="runeId"></param>
    /// <param name="abstractRuneConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveRuneConfig(string userId, string runeId, AbstractRuneConfig abstractRuneConfig) =>
        await this.SaveConfig(this.ForRune, userId, runeId, abstractRuneConfig);

    /// <summary>
    /// 删除全局的工作流配置
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="workflowId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteWorkflowConfig(string userId, string workflowId) =>
        await this.DeleteConfig(this.ForWorkflow, userId, workflowId);

    /// <summary>
    /// 删除全局的枢机配置
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="tuumId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteTuumConfig(string userId, string tuumId) =>
        await this.DeleteConfig(this.ForTuum, userId, tuumId);

    /// <summary>
    /// 删除全局的符文配置
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="runeId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteRuneConfig(string userId, string runeId) =>
        await this.DeleteConfig(this.ForRune, userId, runeId);

    private async Task<Result<T>> FindConfig<T>(ScopeTemplate template, string userId, string configId)
    {
        // a. 从工厂获取用户专属的存储实例
        var userStorageResult = await this.UserStorageFactory.GetFinalStorageForUserAsync(userId, template);
        if (userStorageResult.TryGetError(out var error, out var finalStorage))
            return error;

        // c. 使用最终实例进行操作
        var result = await finalStorage.LoadAllAsync<T>(MakeFileName(configId));
        if (result.TryGetError(out error, out var value))
            return error;
        if (value is null)
            return NormalError.NotFound($"找不到指定的配置:{Path.Combine(finalStorage.WorkPath, configId)}.json");

        return Result.Ok(value);
    }

    private async Task<DictionaryResult<string, T>> FindAllConfig<T>(ScopeTemplate template, string userId)
    {
        var userStorageResult = await this.UserStorageFactory.GetFinalStorageForUserAsync(userId, template);
        if (userStorageResult.TryGetError(out var error, out var finalStorage))
            return DictionaryResult<string, T>.Fail(error);

        var fileListResult = await finalStorage.ListFileNamesAsync();
        if (fileListResult.TryGetError(out error, out var allFileNames))
            return DictionaryResult<string, T>.Fail(error);

        // 1. 为每个文件名创建一个加载任务。
        //    这个任务会异步执行 FindConfig，并返回一个包含 ID 和结果的元组。
        var loadTasks = allFileNames.Select(async fileName =>
        {
            string id = GetIdFromFileName(fileName);
            // 注意：这里我们不再直接 await FindConfig，而是让它返回一个 Task<Result<T>>
            var configResult = await this.FindConfig<T>(template, userId, id);
            return (Id: id, Result: configResult);
        }).ToList();

        // 2. 使用 Task.WhenAll 并行执行所有加载任务。
        //    这将返回一个包含所有结果元组的数组。
        var resultsArray = await Task.WhenAll(loadTasks);

        // 3. 将结果数组转换为字典。
        var configResults = resultsArray.ToDictionary(
            keySelector: tuple => tuple.Id,
            elementSelector: tuple => tuple.Result
        );

        return configResults;
    }

    private async Task<Result> DeleteConfig(ScopeTemplate template, string userId, string configId)
    {
        var userStorageResult = await this.UserStorageFactory.GetFinalStorageForUserAsync(userId, template);
        if (userStorageResult.TryGetError(out var error, out var finalStorage))
            return error;

        return await finalStorage.DeleteFileAsync(MakeFileName(configId));
    }

    private async Task<Result> SaveConfig<T>(ScopeTemplate template, string userId, string configId, T config)
    {
        var userStorageResult = await this.UserStorageFactory.GetFinalStorageForUserAsync(userId, template);
        if (userStorageResult.TryGetError(out var error, out var finalStorage))
            return error;

        return await finalStorage.SaveAllAsync(config, MakeFileName(configId));
    }
}