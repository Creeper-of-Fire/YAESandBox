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
    public required string ConfigId { get; init; }

    /// <summary>
    /// 步骤的AI配置，如果不存在，则这个模块不需要AI处理
    /// </summary>
    public StepAiConfig? StepAiConfig { get; init; }

    /// <summary>
    /// 按顺序执行的模块列表。
    /// StepProcessor 在执行时会严格按照此列表的顺序执行模块。
    /// </summary>
    [Required]
    public List<AbstractModuleConfig> Modules { get; init; } = [];

    /// <summary>
    /// 定义了此步骤如何将其内部变量暴露到工作流的全局变量池。
    /// Key: 全局变量名 (在工作流中使用的名字)
    /// Value: 步骤内部的变量名 (由模块产生的名字)
    /// </summary>
    /// <example>
    /// "final_greeting": "module_A_raw_text"
    /// 这意味着，将此步骤内部名为 "module_A_raw_text" 的变量，
    /// 以 "final_greeting" 的名字发布到全局。
    /// </example>
    [Required]
    public Dictionary<string, string> OutputMappings { get; init; } = [];
}