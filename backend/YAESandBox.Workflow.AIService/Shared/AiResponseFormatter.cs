using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.AIService.Shared;

/// <summary>
/// 表示从 AI 响应中提取的结构化数据块。
/// 这将思维过程（Reasoning）和最终内容（Content）明确分开。
/// </summary>
/// <param name="Reasoning">思维链的文本。</param>
/// <param name="Content">最终要呈现给用户或下游系统的内容。</param>
public record AiStructuredChunk(string? Reasoning, string? Content)
{
    /// <summary>
    /// 检查此数据块是否不包含任何有效内容。
    /// </summary>
    /// <returns>如果 Reasoning 和 Content 都为 null 或空，则返回 true。</returns>
    public bool IsEmpty() => string.IsNullOrEmpty(this.Reasoning) && string.IsNullOrEmpty(this.Content);

    private const string ThinkTagName = "think";

    /// <summary>
    /// 将结构化数据序列化为带有 &lt;think&gt; 标签的单一字符串。
    /// 这主要用于需要扁平化文本输出的场景。
    /// </summary>
    /// <returns>一个组合了 Reasoning 和 Content 的字符串。</returns>
    public string ToLegacyThinkString()
    {
        // 如果两个都为空，直接返回空字符串，避免不必要的 StringBuilder 分配。
        if (this.IsEmpty())
        {
            return string.Empty;
        }

        // 使用 StringBuilder 以获得最佳性能
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(this.Reasoning))
        {
            sb.Append($"<{ThinkTagName}>").Append(this.Reasoning).Append($"</{ThinkTagName}>");
        }

        if (!string.IsNullOrEmpty(this.Content))
        {
            sb.Append(this.Content);
        }

        return sb.ToString();
    }
}

/// <summary>
/// 一个静态工具类，用于格式化实现了 IAiResponseMessagePart 接口的AI响应。
/// 它将内容提取和格式化的通用逻辑（如包裹思维链标签）封装起来，避免在每个Processor中重复。
/// </summary>
public static class AiResponseFormatter
{
    /// <summary>
    /// 从一个AI响应片段中提取、格式化内容，并调用流式回调。
    /// </summary>
    /// <param name="part">AI响应的一部分，如流式delta。必须实现IAiResponseMessagePart接口。</param>
    /// <param name="callback">用于接收格式化后文本块的回调函数。</param>
    public static async Task<Result> FormatAndInvoke(IAiResponseMessagePart? part, StreamRequestCallBack callback)
    {
        if (part is null)
            return Result.Ok();

        var combinedText = GetFormattedContent(part);

        if (!combinedText.IsEmpty())
        {
            return await callback.OnChunkReceivedAsync(combinedText);
        }

        return Result.Ok();
    }

    /// <summary>
    /// 从一个AI响应片段中提取并格式化内容为一个字符串。
    /// 此方法主要用于非流式请求，一次性获取完整格式化后的内容。
    /// </summary>
    /// <param name="part">AI响应的一部分，如完整的message。必须实现IAiResponseMessagePart接口。</param>
    /// <returns>格式化后的完整字符串。</returns>
    public static AiStructuredChunk GetFormattedContent(IAiResponseMessagePart? part)
    {
        if (part is null)
            return new AiStructuredChunk(null, null);

        string? reasoning = part.GetReasoningContent();
        string? content = part.GetContent();

        return new AiStructuredChunk(reasoning, content);
    }
}