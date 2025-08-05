using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Tuum;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;

namespace YAESandBox.Workflow.Core;

/// <summary>
/// 工作流配置服务，主要承担全局模板的管理与访问。
/// 注意：此服务不参与运行时的配置解析。工作流执行时依赖的是前端提供的、包含所有内联子配置的完整配置快照。
///
/// 全局模板（如符文模板、祝祷模板、完整工作流模板）提供可复用的配置蓝图。
/// 从模板拷贝时，其内容会被复制到具体的配置中，并为所有新生成的配置实例（包括配置自身及其内嵌子配置）分配全新的唯一ID。
///
/// 工作流的模板有所不同，它在前端会区分“运行”和“拷贝”。在运行时，它并不会重新分配子配置的ID，因为它自身会被分配一个ID用以区分。
///
/// 不论什么情况，后端都不会对ID进行任何的修改，那是前端的工作。
/// </summary>
/// <param name="generalJsonStorage"></param>
public class WorkflowConfigFileService(IGeneralJsonStorage generalJsonStorage)
{
    private static string MakeFileName(string id) => $"{id}.json";
    private static string GetIdFromFileName(string fileName) => Path.GetFileNameWithoutExtension(fileName);
    private const string MainDirectory = "WorkflowConfigurations";
    private const string WorkflowDirectory = "GlobalWorkflows";
    private const string TuumDirectory = "GlobalTuums";
    private const string RuneDirectory = "GlobalRunes";

    private ScopedJsonStorage ForWorkflow { get; } =
        generalJsonStorage.ForConfig().CreateScope(MainDirectory).CreateScope(WorkflowDirectory);

    private ScopedJsonStorage ForTuum { get; } =
        generalJsonStorage.ForConfig().CreateScope(MainDirectory).CreateScope(TuumDirectory);

    private ScopedJsonStorage ForRune { get; } =
        generalJsonStorage.ForConfig().CreateScope(MainDirectory).CreateScope(RuneDirectory);


    /// <summary>
    /// 只在全局的工作流配置中查找，不查找内联的私有部分（虽然对于工作流没有这部分，但是出于整齐的考虑还是这么写了）
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="workflowId"></param>
    /// <returns></returns>
    internal async Task<Result<WorkflowConfig>> FindWorkflowConfig(string userId, string workflowId) =>
        await FindConfig<WorkflowConfig>(this.ForWorkflow, userId, workflowId);

    /// <summary>
    /// 只在全局的祝祷配置中查找，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="tuumId"></param>
    /// <returns></returns>
    internal async Task<Result<TuumConfig>> FindTuumConfig(string userId, string tuumId) =>
        await FindConfig<TuumConfig>(this.ForTuum, userId, tuumId);

    /// <summary>
    /// 只在全局的符文配置中查找，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="runeId"></param>
    /// <returns></returns>
    internal async Task<Result<AbstractRuneConfig>> FindRuneConfig(string userId, string runeId) =>
        await FindConfig<AbstractRuneConfig>(this.ForRune, userId, runeId);

    /// <summary>
    /// 只寻找所有的全局工作流配置，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, WorkflowConfig>> FindAllWorkflowConfig(string userId) =>
        await FindAllConfig<WorkflowConfig>(this.ForWorkflow, userId);

    /// <summary>
    /// 只寻找所有的全局祝祷配置，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, TuumConfig>> FindAllTuumConfig(string userId) =>
        await FindAllConfig<TuumConfig>(this.ForTuum, userId);

    /// <summary>
    /// 只寻找所有的全局符文配置，不查找内联的私有部分
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<DictionaryResult<string, AbstractRuneConfig>> FindAllRuneConfig(string userId) =>
        await FindAllConfig<AbstractRuneConfig>(this.ForRune, userId);

    /// <summary>
    /// 保存工作流配置到全局
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="workflowId"></param>
    /// <param name="workflowConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveWorkflowConfig(string userId, string workflowId, WorkflowConfig workflowConfig) =>
        await SaveConfig(this.ForWorkflow, userId, workflowId, workflowConfig);

    /// <summary>
    /// 保存祝祷配置到全局
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="tuumId"></param>
    /// <param name="tuumConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveTuumConfig(string userId, string tuumId, TuumConfig tuumConfig) =>
        await SaveConfig(this.ForTuum, userId, tuumId, tuumConfig);

    /// <summary>
    /// 保存符文配置到全局
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="runeId"></param>
    /// <param name="abstractRuneConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveRuneConfig(string userId, string runeId, AbstractRuneConfig abstractRuneConfig) =>
        await SaveConfig(this.ForRune, userId, runeId, abstractRuneConfig);

    /// <summary>
    /// 删除全局的工作流配置
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="workflowId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteWorkflowConfig(string userId, string workflowId) =>
        await DeleteConfig(this.ForWorkflow, userId, workflowId);

    /// <summary>
    /// 删除全局的祝祷配置
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="tuumId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteTuumConfig(string userId, string tuumId) =>
        await DeleteConfig(this.ForTuum, userId, tuumId);

    /// <summary>
    /// 删除全局的符文配置
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="runeId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteRuneConfig(string userId, string runeId) =>
        await DeleteConfig(this.ForRune, userId, runeId);

    private static async Task<Result<T>> FindConfig<T>(ScopedJsonStorage scopedJsonStorage, string userId, string configId)
    {
        var result = await scopedJsonStorage.LoadAllAsync<T>(MakeFileName(configId), userId);
        if (result.TryGetError(out var error, out var value))
            return error;
        if (value is null)
            return NormalError.NotFound($"找不到指定的符文配置:{Path.Combine(scopedJsonStorage.WorkPath, configId)}。");

        return Result.Ok(value);
    }

    private static async Task<DictionaryResult<string, T>> FindAllConfig<T>(ScopedJsonStorage scopedJsonStorage, string userId)
    {
        var result = await scopedJsonStorage.ListFileNamesAsync(null,userId);
        if (result.TryGetError(out var error, out var allFileNames))
            return DictionaryResult<string, T>.Fail(error);
        Dictionary<string, Result<T>> configResults = [];
        foreach (string id in allFileNames.Select(GetIdFromFileName))
        {
            var configResult = await FindConfig<T>(scopedJsonStorage, userId, id);
            configResults.Add(id, configResult);
        }

        return configResults;
    }

    private static async Task<Result> DeleteConfig(ScopedJsonStorage scopedJsonStorage, string userId, string configId) =>
        await scopedJsonStorage.DeleteFileAsync(MakeFileName(configId), userId);

    private static async Task<Result> SaveConfig<T>(ScopedJsonStorage scopedJsonStorage, string userId, string configId, T config) =>
        await scopedJsonStorage.SaveAllAsync(config, MakeFileName(configId), userId);
}