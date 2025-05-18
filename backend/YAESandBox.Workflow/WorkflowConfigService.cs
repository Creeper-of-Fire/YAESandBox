using FluentResults;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Module;
using YAESandBox.Workflow.Step;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;

namespace YAESandBox.Workflow;

public class WorkflowConfigService
{
    private static string MakeFileName(string id) => $"{id}.json";
    private const string MainSaveDirectory = "WorkflowConfigurations";
    private const string WorkflowDirectory = "Workflows";
    private const string StepDirectory = "Steps";
    private const string ModuleDirectory = "Modules";
    private IGeneralJsonStorage GeneralJsonStorage { get; }

    private ScopedJsonStorageStateless ForWorkflow { get; }
    private ScopedJsonStorageStateless ForStep { get; }
    private ScopedJsonStorageStateless ForModule { get; }

    public WorkflowConfigService(IGeneralJsonStorage generalJsonStorage)
    {
        this.GeneralJsonStorage = generalJsonStorage;
        this.ForWorkflow = this.GeneralJsonStorage.ForConfig().CreateScope(MainSaveDirectory, WorkflowDirectory);
        this.ForStep = this.GeneralJsonStorage.ForConfig().CreateScope(MainSaveDirectory, StepDirectory);
        this.ForModule = this.GeneralJsonStorage.ForConfig().CreateScope(MainSaveDirectory, ModuleDirectory);
    }

    public async Task<Result<WorkflowProcessorConfig>> FindWorkflowProcessorConfig(string workflowId)
    {
        var result = await this.ForWorkflow.LoadAllAsync<WorkflowProcessorConfig>(MakeFileName(workflowId));
        if (!result.TryGetValue(out var value))
            return result.ToResult();

        if (value == null)
            return ServerError.NotFound($"找不到指定的工作流配置:{workflowId}。");
        return value;
    }

    public StepProcessorConfig FindStepProcessorConfig(string stepId)
    {
        throw new NotImplementedException();
    }

    public IModuleConfig FindModuleConfig(string moduleId)
    {
        throw new NotImplementedException();
    }

    public void SaveModuleConfig(string moduleId, IModuleConfig moduleConfig)
    {
        throw new NotImplementedException();
    }

    public void SaveWorkflowProcessorConfig(string workflowId, WorkflowProcessorConfig workflowProcessorConfig)
    {
        throw new NotImplementedException();
    }

    public void SaveStepProcessorConfig(string stepId, StepProcessorConfig stepProcessorConfig)
    {
        throw new NotImplementedException();
    }
}