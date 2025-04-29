// --- File: YAESandBox.Workflow/Common/ScriptConfig.cs ---
namespace YAESandBox.Workflow.Common;

/// <summary>
/// 代表一段可执行的 C# 脚本配置。
/// </summary>
/// <param name="ScriptContent">C# 脚本的文本内容。</param>
/// <param name="Description">(可选) 脚本功能的描述。</param>
public record ScriptConfig(
    string ScriptContent,
    string? Description = null
);