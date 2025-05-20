using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.Step;

namespace YAESandBox.Workflow.Module;

internal abstract record AbstractModuleConfig<T>(string ModuleType) : IModuleConfig
    where T : IModuleProcessor
{
    /// <inheritdoc />
    [Required]
    [HiddenInSchema(true)]
    public string ModuleType { get; init; } = ModuleType;

    public IModuleProcessor ToModule(StepProcessor.StepProcessorContent stepProcessor) =>
        this.ToCurrentModule(stepProcessor);

    protected abstract T ToCurrentModule(StepProcessor.StepProcessorContent stepProcessor);
}

[JsonConverter(typeof(ModuleConfigConverter))]
public interface IModuleConfig
{
    /// <summary>
    /// 模块的类型
    /// </summary>
    public string ModuleType { get; init; }

    internal IModuleProcessor ToModule(StepProcessor.StepProcessorContent stepProcessor);
}