using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.Step;

namespace YAESandBox.Workflow.Module;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "ModuleType")]
[JsonDerivedType(typeof(AiModuleConfig), nameof(AiModuleConfig))]
internal abstract record AbstractModuleConfig<T>(string ModuleType) : IModuleConfig
    where T : IModuleProcessor
{
    /// <summary>
    /// 模块的类型
    /// </summary>
    [Required]
    [HiddenInSchema(true)]
    public string ModuleType { get; init; } = ModuleType;

    public IModuleProcessor ToModule(StepProcessor.StepProcessorContent stepProcessor) =>
        this.ToCurrentModule(stepProcessor);

    internal abstract T ToCurrentModule(StepProcessor.StepProcessorContent stepProcessor);
}

public interface IModuleConfig
{
    internal IModuleProcessor ToModule(StepProcessor.StepProcessorContent stepProcessor);
}