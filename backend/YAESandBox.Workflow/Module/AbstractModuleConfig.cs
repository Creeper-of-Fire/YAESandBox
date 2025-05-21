using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Step.StepProcessor;

namespace YAESandBox.Workflow.Module;

internal abstract record AbstractModuleConfig<T> : IModuleConfig
    where T : IWithDebugDto<IModuleProcessorDebugDto>
{
    /// <inheritdoc />
    [Required]
    [HiddenInSchema(true)]
    public string ModuleType { get; init; } = nameof(T);

    public async Task<IWithDebugDto<IModuleProcessorDebugDto>> ToModuleAsync(
        WorkflowConfigService workflowConfigService) =>
        await this.ToCurrentModuleAsync(workflowConfigService);

    protected abstract Task<T> ToCurrentModuleAsync(WorkflowConfigService workflowConfigService);
}

[JsonConverter(typeof(ModuleConfigConverter))]
public interface IModuleConfig
{
    /// <summary>
    /// 模块的类型
    /// </summary>
    public string ModuleType { get; init; }

    internal Task<IWithDebugDto<IModuleProcessorDebugDto>> ToModuleAsync(WorkflowConfigService workflowConfigService);
}