// --- File: YAESandBox.Workflow/Common/StepConfig.cs ---

// 引用新命名空间

using YAESandBox.Workflow.Common.AiConfig;

namespace YAESandBox.Workflow.Common;

/// <summary>
/// 代表工作流中的一个步骤配置。
/// </summary>
/// <param name="Id">步骤的唯一标识符。</param>
/// <param name="Description">(可选) 步骤功能的描述。</param>
/// <param name="PromptPreparationScripts">提示词准备阶段执行的脚本列表。</param>
/// <param name="MainProcessingScript">主处理逻辑脚本。</param>
/// <param name="AiConfig">(可选) AI 服务交互配置。如果为 null，则为纯脚本步骤。</param>
/// <param name="StreamCallbackScript">(可选, 仅当 AiConfig?.IsStreaming 为 true 时相关) 处理 AI 流式响应的回调脚本。</param>
public record StepConfig(
    string Id,
    string? Description,
    List<ScriptConfig> PromptPreparationScripts,
    ScriptConfig MainProcessingScript,
    IAiConfig? AiConfig = null, // 使用接口类型
    ScriptConfig? StreamCallbackScript = null
)
{
    public bool IsAiStep => AiConfig != null;
    // StreamCallbackScript 的有效性检查也应基于 AiConfig.IsStreaming
    public bool RequiresStreamCallback => AiConfig?.IsStreaming ?? false;
}