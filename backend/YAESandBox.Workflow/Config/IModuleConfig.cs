using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.Config;

/// <summary>
/// 模组的配置
/// </summary>
[JsonConverter(typeof(ModuleConfigConverter))]
public interface IModuleConfig
{
    /// <summary>
    /// 唯一的 ID，在拷贝时也需要更新
    /// </summary>
    [Required]
    [HiddenInSchema(true)]
    public string ConfigId { get; init; }

    /// <summary>
    /// 模块的类型
    /// </summary>
    [Required]
    [HiddenInSchema(true)]
    public string ModuleType { get; init; }

    internal IWithDebugDto<IModuleProcessorDebugDto> ToModuleProcessor();
}

internal abstract record AbstractModuleConfig<T> : IModuleConfig where T : IWithDebugDto<IModuleProcessorDebugDto>
{
    /// <inheritdoc />
    public string ConfigId { get; init; } = string.Empty;

    /// <inheritdoc />
    public string ModuleType { get; init; } = nameof(T);

    public IWithDebugDto<IModuleProcessorDebugDto> ToModuleProcessor() =>
        this.ToCurrentModule();

    protected abstract T ToCurrentModule();
}