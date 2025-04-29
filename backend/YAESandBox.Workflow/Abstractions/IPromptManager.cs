// --- File: YAESandBox.Workflow/Abstractions/IPromptManager.cs ---
using System.Collections.Generic;
using YAESandBox.Workflow.Common; // For PromptMessage

namespace YAESandBox.Workflow.Abstractions;

/// <summary>
/// 管理当前步骤的提示词列表。
/// </summary>
public interface IPromptManager
{
    /// <summary>
    /// 添加一条带模板的消息。模板将使用 Globals 和 Locals 变量进行渲染。
    /// </summary>
    /// <param name="templateContent">包含 {{变量名}} 的消息内容。</param>
    /// <param name="role">角色 (System, User, Assistant等)。</param>
    void Add(string templateContent, string role);

    /// <summary>
    /// 添加一条无需模板渲染的纯文本消息。
    /// </summary>
    void AddRaw(string rawContent, string role);

    /// <summary>
    /// 清空当前提示词列表。
    /// </summary>
    void Clear();

    /// <summary>
    /// 获取当前已构建并渲染完成的所有提示词消息。
    /// </summary>
    /// <returns>只读的提示消息列表。</returns>
    IReadOnlyList<PromptMessage> GetRenderedMessages();

    /// <summary>
    /// 将当前提示词列表 (原始模板内容) 保存到工作流全局变量。
    /// </summary>
    /// <param name="variableName">目标全局变量的名称。</param>
    void SaveToGlobals(string variableName);

    /// <summary>
    /// 从工作流全局变量加载提示词列表 (原始模板内容)，替换当前列表。
    /// </summary>
    /// <param name="variableName">源全局变量的名称。</param>
    void LoadFromGlobals(string variableName);
}