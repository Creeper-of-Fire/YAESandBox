namespace YAESandBox.Workflow.ExactRune.SillyTavern;


/// <summary>
/// 存储模板填充后的结果。
/// </summary>
public record FillResult
{
    /// <summary>
    /// 已被变量填充过的新的 PromptTemplateItem 实例。
    /// </summary>
    public required PromptTemplateItem FilledItem { get; init; }

    /// <summary>
    /// 在填充过程中通过 {{setvar::...}} 产生的新变量字典。
    /// </summary>
    public Dictionary<string, string> ProducedVariables { get; init; } = [];
}