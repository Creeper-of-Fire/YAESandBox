using FluentResults;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Module;
using YAESandBox.Workflow.Step;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;

namespace YAESandBox.Workflow;

public class WorkflowConfigService(IGeneralJsonStorage generalJsonStorage)
{
    private static string MakeFileName(string id) => $"{id}.json";
    private static string GetIdFromFileName(string fileName) => Path.GetFileNameWithoutExtension(fileName);
    private const string MainDirectory = "WorkflowConfigurations";
    private const string WorkflowDirectory = "Workflows";
    private const string StepDirectory = "Steps";
    private const string ModuleDirectory = "Modules";

    private IGeneralJsonStorage GeneralJsonStorage { get; } = generalJsonStorage;

    private ScopedJsonStorage ForWorkflow { get; } =
        generalJsonStorage.ForConfig().CreateScope(MainDirectory).CreateScope(WorkflowDirectory);

    private ScopedJsonStorage ForStep { get; } = 
        generalJsonStorage.ForConfig().CreateScope(MainDirectory).CreateScope(StepDirectory);
    private ScopedJsonStorage ForModule { get; } = 
        generalJsonStorage.ForConfig().CreateScope(MainDirectory).CreateScope(ModuleDirectory);


    internal async Task<Result<WorkflowProcessorConfig>> FindWorkflowProcessorConfig(string workflowId) =>
        await FindConfig<WorkflowProcessorConfig>(this.ForWorkflow, workflowId);

    internal async Task<Result<StepProcessorConfig>> FindStepProcessorConfig(string stepId) =>
        await FindConfig<StepProcessorConfig>(this.ForStep, stepId);

    internal async Task<Result<IModuleConfig>> FindModuleConfig(string moduleId) =>
        await FindConfig<IModuleConfig>(this.ForModule, moduleId);

    public async Task<Result<IEnumerable<WorkflowProcessorConfig>>> FindAllWorkflowProcessorConfig() =>
        await FindAllConfig<WorkflowProcessorConfig>(this.ForWorkflow);

    public async Task<Result<IEnumerable<StepProcessorConfig>>> FindAllStepProcessorConfig() =>
        await FindAllConfig<StepProcessorConfig>(this.ForStep);

    public async Task<Result<IEnumerable<IModuleConfig>>> FindAllModuleConfig() =>
        await FindAllConfig<IModuleConfig>(this.ForModule);

    public async Task<Result> SaveWorkflowProcessorConfig(string workflowId, WorkflowProcessorConfig workflowProcessorConfig) =>
        await this.ForWorkflow.SaveAllAsync(workflowProcessorConfig, MakeFileName(workflowId));

    public async Task<Result> SaveStepProcessorConfig(string stepId, StepProcessorConfig stepProcessorConfig) =>
        await this.ForStep.SaveAllAsync(stepProcessorConfig, MakeFileName(stepId));

    public async Task<Result> SaveModuleConfig(string moduleId, IModuleConfig moduleConfig) =>
        await this.ForModule.SaveAllAsync(moduleConfig, MakeFileName(moduleId));

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