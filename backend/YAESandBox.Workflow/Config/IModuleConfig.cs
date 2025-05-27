using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.Module;

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
    public string InstanceId { get; init; }

    /// <summary>
    /// 模块的类型
    /// </summary>
    [Required]
    [HiddenInSchema(true)]
    public string ModuleType { get; init; }

    internal IWithDebugDto<IModuleProcessorDebugDto> ToModuleProcessor();
}