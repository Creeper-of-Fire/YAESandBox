// FormFieldSchema.cs

using System.ComponentModel.DataAnnotations;

namespace YAESandBox.Workflow.AIService.AiConfigSchema;

/// <summary>
/// 代表一个选择项，用于下拉列表或单选/复选按钮组。
/// </summary>
public class SelectOption
{
    /// <summary>
    /// 选项的实际值。
    /// </summary>
    [Required]
    public object Value { get; init; } = string.Empty;

    /// <summary>
    /// 选项在UI上显示的文本。
    /// </summary>
    [Required]
    public string Label { get; init; } = string.Empty;
}
