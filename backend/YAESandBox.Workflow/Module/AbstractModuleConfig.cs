using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.Attributes;
using static YAESandBox.Workflow.Step.StepProcessor;

namespace YAESandBox.Workflow.Module;

internal abstract record AbstractModuleConfig<T>(string ModuleType) : IModuleConfig
    where T : IModuleProcessor
{
    /// <inheritdoc />
    [Required]
    [HiddenInSchema(true)]
    public string ModuleType { get; init; } = ModuleType;

    public async Task<IModuleProcessor> ToModuleAsync(WorkflowConfigService workflowConfigService, StepProcessorContent stepProcessor) =>
        await this.ToCurrentModuleAsync(workflowConfigService, stepProcessor);

    protected abstract Task<T> ToCurrentModuleAsync(WorkflowConfigService workflowConfigService, StepProcessorContent stepProcessor);
}

[JsonConverter(typeof(ModuleConfigConverter))]
public interface IModuleConfig
{
    /// <summary>
    /// 模块的类型
    /// </summary>
    public string ModuleType { get; init; }

    internal Task<IModuleProcessor> ToModuleAsync(WorkflowConfigService workflowConfigService, StepProcessorContent stepProcessor);
}