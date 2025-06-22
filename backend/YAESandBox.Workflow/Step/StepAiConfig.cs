using System.ComponentModel.DataAnnotations;

namespace YAESandBox.Workflow.Step;

/// <summary>
/// 步骤本身的 AI 配置。
/// </summary>
public record StepAiConfig
{
    /// <summary>AI服务的配置的UUID</summary>
    public string? AiProcessorConfigUuid { get; init; }

    /// <summary>当前选中的AI模型的类型名</summary>
    public string? SelectedAiModuleType { get; init; }

    /// <summary>是否为流式传输</summary>
    [Required]
    public bool IsStream { get; init; } = false;
}