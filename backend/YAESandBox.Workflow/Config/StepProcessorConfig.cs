using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.Step;

namespace YAESandBox.Workflow.Config;

public record StepProcessorConfig
{
    /// <summary>
    /// 唯一的 ID，在拷贝时也需要更新
    /// </summary>
    [Required]
    [HiddenInSchema(true)]
    public required string InstanceId { get; init; }

    /// <summary>
    /// 步骤的AI配置，如果不存在，则这个模块不需要AI处理
    /// </summary>
    public StepAiConfig? StepAiConfig { get; init; }

    /// <summary>
    /// 按顺序执行的模块列表。
    /// StepProcessor 在执行时会严格按照此列表的顺序执行模块。
    /// </summary>
    public List<IModuleConfig> Modules { get; init; } = [];
}