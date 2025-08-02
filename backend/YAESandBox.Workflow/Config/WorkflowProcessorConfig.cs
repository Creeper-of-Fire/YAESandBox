using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Schema.Attributes;

namespace YAESandBox.Workflow.Config;

/// <summary>
/// 工作流的配置
/// </summary>
public record WorkflowProcessorConfig
{
    /// <summary>
    /// 名字
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 声明此工作流启动时需要提供的触发参数列表。
    /// 用于校验和前端提示。
    /// </summary>
    [Required]
    public List<string> TriggerParams { get; init; } = [];

    /// <summary>
    /// 一个工作流含有的祝祷（有序）
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public List<TuumProcessorConfig> Tuums { get; init; } = [];
}