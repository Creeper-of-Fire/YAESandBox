// --- File: YAESandBox.Workflow/Abstractions/ITemplateRenderer.cs ---
using System.Collections.Generic;

namespace YAESandBox.Workflow.Abstractions;

/// <summary>
/// 负责将包含模板变量的字符串进行渲染。
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// 使用提供的变量上下文渲染模板字符串。
    /// </summary>
    /// <param name="template">包含 {{变量名}} 的模板字符串。</param>
    /// <param name="variables">包含变量名和值的字典 (结合 Globals 和 Locals)。</param>
    /// <returns>渲染后的字符串。</returns>
    string Render(string template, IReadOnlyDictionary<string, object?> variables);
}