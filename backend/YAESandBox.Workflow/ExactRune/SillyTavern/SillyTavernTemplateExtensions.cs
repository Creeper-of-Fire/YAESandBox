using System.Text.RegularExpressions;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Core.VarSpec;

namespace YAESandBox.Workflow.ExactRune.SillyTavern;

/// <summary>
/// 酒馆模版处理
/// </summary>
public static partial class SillyTavernTemplateExtensions
{
    [GeneratedRegex(@"\{\{getvar::([^\}]+?)\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CompiledGetVarRegex();

    [GeneratedRegex(@"\{\{setvar::([^:]+?)::([^\}]+?)\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CompiledSetVarRegex();

    [GeneratedRegex(@"\{\{addvar::([^:]+?)::([^\}]+?)\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CompiledAddVarRegex();

    [GeneratedRegex(@"\{\{//.*?\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CompiledCommentRegex();


    [GeneratedRegex(@"\{\{random::(.*?)\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CompiledRandomDoubleColonRegex();

    [GeneratedRegex(@"\{\{random:\((.*?)\)\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CompiledRandomParenRegex();

    [GeneratedRegex(@"\{\{roll:\((.*?)\)\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CompiledRollRegex();

    [GeneratedRegex(@"\{\{time_UTC([+\-]\d+)\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CompiledTimeUtcRegex();

    [GeneratedRegex(@"^(\d*)d(\d+)([\+\-]\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CompiledDiceRollFormulaRegex();

    [GeneratedRegex(@"\s*\{\{trim\}\}\s*", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex TrimRegex();


    private static readonly Regex GetVarRegex = CompiledGetVarRegex();
    private static readonly Regex SetVarRegex = CompiledSetVarRegex();
    private static readonly Regex AddVarRegex = CompiledAddVarRegex();
    private static readonly Regex CommentRegex = CompiledCommentRegex();
    private static readonly Regex RandomDoubleColonRegex = CompiledRandomDoubleColonRegex();
    private static readonly Regex RandomParenRegex = CompiledRandomParenRegex();
    private static readonly Regex RollRegex = CompiledRollRegex();
    private static readonly Regex TimeUtcRegex = CompiledTimeUtcRegex();
    private static readonly Regex DiceRollFormulaRegex = CompiledDiceRollFormulaRegex();

    /// <summary>
    /// (纯函数) 使用提供的变量填充模板条目，返回一个新的实例。
    /// </summary>
    /// <param name="item">要填充的模板条目。</param>
    /// <param name="variables">用于替换 {{getvar::...}} 的变量字典。</param>
    /// <param name="playerCharacter">用于替换 {{user}} 和 {{persona}} 的玩家角色信息。</param>
    /// <param name="targetCharacter">用于替换 {{char}} 和 {{description}} 的目标角色信息。</param>
    /// <param name="history">完整的聊天历史记录，用于 {{last...Message}} 等宏。</param>
    /// <returns>一个包含填充后条目和新产生变量的 `FillResult` 对象。</returns>
    public static FillResult FillTemplate(
        this PromptTemplateItem item,
        IDictionary<string, string> variables,
        ThingInfo playerCharacter,
        ThingInfo targetCharacter,
        IReadOnlyList<RoledPromptDto> history)
    {
        if (item.IsMark || item.Content is null)
        {
            return new FillResult { FilledItem = item };
        }

        string currentContent = item.Content;
        var producedVariables = new Dictionary<string, string>();

        // --- 1. 预处理和初始化 ---
        var executionTimeUtc = DateTime.UtcNow; // 捕获单次执行的统一时间
        var localTime = executionTimeUtc.ToLocalTime();
        // 移除注释
        currentContent = CommentRegex.Replace(currentContent, string.Empty);

        // 处理 {{trim}}
        // 这个宏比较特殊，它会移除自身及周围的空白字符。
        currentContent = TrimRegex().Replace(currentContent, " ");


        // --- 求值循环 ---
        // 通过多次扫描，允许宏的结果被其他宏使用。
        // 例如，第一轮处理 setvar, 第二轮处理 getvar。
        for (int i = 0; i < 5; i++) // 设置一个安全上限防止无限循环
        {
            bool changedInThisPass = false;

            // 2.1 处理变量设置宏 (setvar, addvar)
            currentContent = SetVarRegex.Replace(currentContent, match =>
            {
                string varName = match.Groups[1].Value.Trim();
                string varValue = match.Groups[2].Value;
                producedVariables[varName] = varValue;
                changedInThisPass = true;
                return string.Empty;
            });

            currentContent = AddVarRegex.Replace(currentContent, match =>
            {
                string varName = match.Groups[1].Value.Trim();
                string valueToAdd = match.Groups[2].Value;
                // 优先从本次新产生的变量中查找，再从传入的变量中查找
                string existingValue = producedVariables.GetValueOrDefault(varName,
                    variables.TryGetValue(varName, out string? value) ? value : string.Empty);
                producedVariables[varName] = existingValue + valueToAdd;
                changedInThisPass = true;
                return string.Empty;
            });

            // 2.2 替换 getvar
            // 合并已有变量和本轮新产生的变量，以供 getvar 使用
            var availableVars = new Dictionary<string, string>(variables);
            foreach ((string key, string value) in producedVariables)
            {
                availableVars[key] = value;
            }
            currentContent = GetVarRegex.Replace(currentContent, match =>
            {
                string varName = match.Groups[1].Value.Trim();
                if (availableVars.TryGetValue(varName, out string? value))
                {
                    changedInThisPass = true;
                    return value;
                }

                return string.Empty;
            });

            // 2.3 替换带参数的、需要计算的宏
            currentContent = RandomParenRegex.Replace(currentContent, match =>
            {
                string[] options = match.Groups[1].Value.Split(',');
                changedInThisPass = true;
                return options.Length > 0 ? options[Random.Shared.Next(options.Length)].Trim() : string.Empty;
            });
            currentContent = RandomDoubleColonRegex.Replace(currentContent, match =>
            {
                string[] options = match.Groups[1].Value.Split("::");
                changedInThisPass = true;
                return options.Length > 0 ? options[Random.Shared.Next(options.Length)].Trim() : string.Empty;
            });
            currentContent = RollRegex.Replace(currentContent, match =>
            {
                changedInThisPass = true;
                return ProcessDiceRoll(match.Groups[1].Value.Trim());
            });
            currentContent = TimeUtcRegex.Replace(currentContent, match =>
            {
                if (int.TryParse(match.Groups[1].Value, out int offset))
                {
                    changedInThisPass = true;
                    return executionTimeUtc.AddHours(offset).ToString("HH:mm:ss");
                }

                return match.Value; // 解析失败则返回原样
            });

            // TODO: {{timeDiff}} 需要更复杂的解析器，暂时不实现。
            // TODO: {{idle_duration}} 需要聊天记录带时间戳，当前数据结构不支持。

            if (!changedInThisPass && i > 0) break; // 如果一轮下来没有任何变化，提前退出
        }

        // --- 3. 最终替换无参数的上下文宏 ---
        string lastUserMessage = history.LastOrDefault(p => p.Role == PromptRoleType.User)?.Content ?? string.Empty;
        string lastCharMessage = history.LastOrDefault(p => p.Role == PromptRoleType.Assistant)?.Content ?? string.Empty;

        currentContent = currentContent.Replace("{{newline}}", "\n", StringComparison.OrdinalIgnoreCase);
        currentContent = currentContent.Replace("{{user}}", playerCharacter.Name, StringComparison.OrdinalIgnoreCase);
        currentContent = currentContent.Replace("{{persona}}", playerCharacter.Description, StringComparison.OrdinalIgnoreCase);
        currentContent = currentContent.Replace("{{char}}", targetCharacter.Name, StringComparison.OrdinalIgnoreCase);
        currentContent = currentContent.Replace("{{description}}", targetCharacter.Description, StringComparison.OrdinalIgnoreCase);
        currentContent = currentContent.Replace("{{lastUserMessage}}", lastUserMessage, StringComparison.OrdinalIgnoreCase);
        currentContent = currentContent.Replace("{{lastCharMessage}}", lastCharMessage, StringComparison.OrdinalIgnoreCase);
        currentContent = currentContent.Replace("{{date}}", localTime.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase);
        currentContent = currentContent.Replace("{{time}}", localTime.ToString("HH:mm:ss"), StringComparison.OrdinalIgnoreCase);


        // --- 4. 返回结果 ---
        var newItem = item with { Content = currentContent.Trim() };

        return new FillResult { FilledItem = newItem, ProducedVariables = producedVariables };
    }

    /// <summary>
    /// 处理 DnD 风格的投骰公式字符串。
    /// </summary>
    /// <param name="formula">例如 "d6", "2d8+5", "1d20-1"</param>
    /// <returns>计算结果的字符串，如果公式无效则返回 "0"。</returns>
    private static string ProcessDiceRoll(string formula)
    {
        var match = DiceRollFormulaRegex.Match(formula);
        if (!match.Success) return "0";

        // 解析骰子数量，默认为1
        _ = int.TryParse(string.IsNullOrEmpty(match.Groups[1].Value) ? "1" : match.Groups[1].Value, out int diceCount);
        // 解析骰子面数
        _ = int.TryParse(match.Groups[2].Value, out int sides);
        // 解析调整值
        _ = int.TryParse(match.Groups[3].Value, out int modifier);

        if (sides <= 0) return "0";

        diceCount = Math.Clamp(diceCount, 1, 100); // 安全限制
        sides = Math.Clamp(sides, 1, 1000); // 安全限制

        int total = 0;
        for (int i = 0; i < diceCount; i++)
        {
            total += Random.Shared.Next(1, sides + 1);
        }

        return (total + modifier).ToString();
    }
}