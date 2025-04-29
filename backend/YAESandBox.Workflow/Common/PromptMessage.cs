// --- File: YAESandBox.Workflow/Common/PromptMessage.cs ---
namespace YAESandBox.Workflow.Common;

/// <summary>
/// 表示一条带有角色的提示消息 (供 PromptManager 使用)
/// </summary>
/// <param name="Role">角色 (例如 "System", "User", "Assistant")</param>
/// <param name="TemplateContent">包含 {{模板变量}} 的原始消息内容</param>
/// <param name="RenderedContent">模板渲染后的最终内容 (由 PromptManager 填充)</param>
public record PromptMessage(
    string Role,
    string TemplateContent,
    string? RenderedContent = null // 初始为 null，渲染后填充
);