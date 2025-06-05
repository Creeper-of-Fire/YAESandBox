using FluentResults;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Module;
using YAESandBox.Workflow.Step;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;

namespace YAESandBox.Workflow.Utility;

/// <summary>
/// 工作流配置服务，主要承担全局模板的管理与访问。
/// 注意：此服务不参与运行时的配置解析。工作流执行时依赖的是前端提供的、包含所有内联子配置的完整配置快照。
///
/// 全局模板（如模块模板、步骤模板、完整工作流模板）提供可复用的配置蓝图。
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
    private const string StepDirectory = "GlobalSteps";
    private const string ModuleDirectory = "GlobalModules";

    // private IGeneralJsonStorage GeneralJsonStorage { get; } = generalJsonStorage;

    private ScopedJsonStorage ForWorkflow { get; } =
        generalJsonStorage.ForConfig().CreateScope(MainDirectory).CreateScope(WorkflowDirectory);

    private ScopedJsonStorage ForStep { get; } =
        generalJsonStorage.ForConfig().CreateScope(MainDirectory).CreateScope(StepDirectory);

    private ScopedJsonStorage ForModule { get; } =
        generalJsonStorage.ForConfig().CreateScope(MainDirectory).CreateScope(ModuleDirectory);


    /// <summary>
    /// 只在全局的工作流配置中查找，不查找内联的私有部分（虽然对于工作流没有这部分，但是出于整齐的考虑还是这么写了）
    /// </summary>
    /// <param name="workflowId"></param>
    /// <returns></returns>
    internal async Task<Result<WorkflowProcessorConfig>> FindWorkflowConfig(string workflowId) =>
        await FindConfig<WorkflowProcessorConfig>(this.ForWorkflow, workflowId);

    /// <summary>
    /// 只在全局的步骤配置中查找，不查找内联的私有部分
    /// </summary>
    /// <param name="stepId"></param>
    /// <returns></returns>
    internal async Task<Result<StepProcessorConfig>> FindStepConfig(string stepId) =>
        await FindConfig<StepProcessorConfig>(this.ForStep, stepId);

    /// <summary>
    /// 只在全局的模块配置中查找，不查找内联的私有部分
    /// </summary>
    /// <returns></returns>
    internal async Task<Result<IModuleConfig>> FindModuleConfig(string moduleId) =>
        await FindConfig<IModuleConfig>(this.ForModule, moduleId);

    /// <summary>
    /// 只寻找所有的全局工作流配置，不查找内联的私有部分
    /// </summary>
    /// <returns></returns>
    public async Task<Result<IEnumerable<WorkflowProcessorConfig>>> FindAllWorkflowConfig() =>
        await FindAllConfig<WorkflowProcessorConfig>(this.ForWorkflow);

    /// <summary>
    /// 只寻找所有的全局步骤配置，不查找内联的私有部分
    /// </summary>
    /// <returns></returns>
    public async Task<Result<IEnumerable<StepProcessorConfig>>> FindAllStepConfig() =>
        await FindAllConfig<StepProcessorConfig>(this.ForStep);

    /// <summary>
    /// 只寻找所有的全局模块配置，不查找内联的私有部分
    /// </summary>
    /// <returns></returns>
    public async Task<Result<IEnumerable<IModuleConfig>>> FindAllModuleConfig() =>
        await FindAllConfig<IModuleConfig>(this.ForModule);

    /// <summary>
    /// 保存工作流配置到全局
    /// </summary>
    /// <param name="workflowId"></param>
    /// <param name="workflowProcessorConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveWorkflowConfig(string workflowId, WorkflowProcessorConfig workflowProcessorConfig) =>
        await this.ForWorkflow.SaveAllAsync(workflowProcessorConfig, MakeFileName(workflowId));

    /// <summary>
    /// 保存步骤配置到全局
    /// </summary>
    /// <param name="stepId"></param>
    /// <param name="stepProcessorConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveStepConfig(string stepId, StepProcessorConfig stepProcessorConfig) =>
        await this.ForStep.SaveAllAsync(stepProcessorConfig, MakeFileName(stepId));

    /// <summary>
    /// 保存模块配置到全局
    /// </summary>
    /// <param name="moduleId"></param>
    /// <param name="moduleConfig"></param>
    /// <returns></returns>
    public async Task<Result> SaveModuleConfig(string moduleId, IModuleConfig moduleConfig) =>
        await this.ForModule.SaveAllAsync(moduleConfig, MakeFileName(moduleId));

    /// <summary>
    /// 删除全局的工作流配置
    /// </summary>
    /// <param name="workflowId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteWorkflowConfig(string workflowId) =>
        await this.ForWorkflow.DeleteFileAsync(MakeFileName(workflowId));

    /// <summary>
    /// 删除全局的步骤配置
    /// </summary>
    /// <param name="stepId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteStepConfig(string stepId) =>
        await this.ForStep.DeleteFileAsync(MakeFileName(stepId));

    /// <summary>
    /// 删除全局的模块配置
    /// </summary>
    /// <param name="moduleId"></param>
    /// <returns></returns>
    public async Task<Result> DeleteModuleConfig(string moduleId) =>
        await this.ForModule.DeleteFileAsync(MakeFileName(moduleId));

    private static async Task<Result<T>> FindConfig<T>(ScopedJsonStorage scopedJsonStorage, string configId)
    {
        var result = await scopedJsonStorage.LoadAllAsync<T>(MakeFileName(configId));
        if (!result.TryGetValue(out var value))
            return result.ToResult();
        if (value is null)
            return NormalError.NotFound($"找不到指定的模块配置:{Path.Combine(scopedJsonStorage.WorkPath, configId)}。");

        return Result.Ok(value);
    }

    private static async Task<Result<IEnumerable<T>>> FindAllConfig<T>(ScopedJsonStorage scopedJsonStorage)
    {
        var result = await scopedJsonStorage.ListFileNamesAsync();
        if (!result.TryGetValue(out var allFileNames))
            return result.ToResult();
        List<T> configs = [];
        List<IError> allErrors = [];
        foreach (string id in allFileNames.Select(GetIdFromFileName))
        {
            var configResult = await FindConfig<T>(scopedJsonStorage, id);
            if (configResult.TryGetValue(out var config))
                configs.Add(config);
            else
                allErrors.AddRange(configResult.Errors);
        }

        if (allErrors.Count > 0)
            return NormalError.Error("成功扫描了目录下全部文件，但是在下一步寻找这些文件时遇到了错误。").WithErrors(allErrors);

        return configs;
    }
}