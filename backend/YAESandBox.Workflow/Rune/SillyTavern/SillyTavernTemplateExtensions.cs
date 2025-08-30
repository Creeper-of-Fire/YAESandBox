using System.Text.RegularExpressions;
using YAESandBox.Workflow.VarSpec;

namespace YAESandBox.Workflow.Rune.SillyTavern;

/// <summary>
/// 酒馆模版处理
/// </summary>
public static partial class SillyTavernTemplateExtensions
{
    [GeneratedRegex(@"\{\{getvar::([^\}]+?)\}\}", RegexOptions.Compiled)]
    private static partial Regex CompiledGetVarRegex();

    [GeneratedRegex(@"\{\{setvar::([^:]+?)::([^\}]+?)\}\}", RegexOptions.Compiled)]
    private static partial Regex CompiledSetVarRegex();

    [GeneratedRegex(@"\{\{//.*?\}\}", RegexOptions.Compiled)]
    private static partial Regex CompiledCommentRegex();

    private static readonly Regex GetVarRegex = CompiledGetVarRegex();
    private static readonly Regex SetVarRegex = CompiledSetVarRegex();
    private static readonly Regex CommentRegex = CompiledCommentRegex();

    /// <summary>
    /// (纯函数) 使用提供的变量填充模板条目，返回一个新的实例。
    /// </summary>
    /// <param name="item">要填充的模板条目。</param>
    /// <param name="variables">用于替换 {{getvar::...}} 的变量字典。</param>
    /// <param name="playerCharacter">用于替换 {{user}} 和 {{persona}} 的玩家角色信息。</param>
    /// <param name="targetCharacter">用于替换 {{char}} 和 {{description}} 的目标角色信息。</param>
    /// <returns>一个包含填充后条目和新产生变量的 `FillResult` 对象。</returns>
    public static FillResult FillTemplate(
        this PromptTemplateItem item,
        IDictionary<string, string> variables,
        ThingInfo playerCharacter,
        ThingInfo targetCharacter)
    {
        if (item.IsMark || item.Content is null)
        {
            return new FillResult { FilledItem = item };
        }

        string currentContent = item.Content;
        var producedVariables = new Dictionary<string, string>();

        // 1. 移除注释
        currentContent = CommentRegex.Replace(currentContent, string.Empty);

        // 2. 处理并移除 {{setvar}}, 同时收集产生的变量
        currentContent = SetVarRegex.Replace(currentContent, match =>
        {
            string varName = match.Groups[1].Value;
            string varValue = match.Groups[2].Value;
            producedVariables[varName] = varValue;
            return string.Empty; // 从内容中移除 setvar 宏
        });

        // 3. 替换 {{getvar}}
        currentContent = GetVarRegex.Replace(currentContent, match =>
        {
            string varName = match.Groups[1].Value;
            // 静默失败：如果找不到变量，则替换为空字符串
            return variables.TryGetValue(varName, out string? value) ? value : string.Empty;
        });

        // 4. 替换硬编码的特殊宏
        currentContent = currentContent.Replace("{{user}}", playerCharacter.Name);
        currentContent = currentContent.Replace("{{persona}}", playerCharacter.Description);
        currentContent = currentContent.Replace("{{char}}", targetCharacter.Name);
        currentContent = currentContent.Replace("{{description}}", targetCharacter.Description);

        // 5. 创建新的、不可变的 DTO 实例
        var newItem = item with { Content = currentContent.Trim() };

        return new FillResult { FilledItem = newItem, ProducedVariables = producedVariables };
    }
}