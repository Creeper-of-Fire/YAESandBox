using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.AIService.Shared;

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

        string combinedText = GetFormattedContent(part);

        if (!string.IsNullOrEmpty(combinedText))
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
    public static string GetFormattedContent(IAiResponseMessagePart? part)
    {
        if (part is null)
            return string.Empty;

        // 使用 StringBuilder 以获得更好的性能，特别是在多次拼接字符串时。
        var sb = new System.Text.StringBuilder();

        string? reasoning = part.GetReasoningContent();
        if (!string.IsNullOrEmpty(reasoning))
        {
            sb.Append("<think>").Append(reasoning).Append("</think>");
        }

        string? content = part.GetContent();
        if (!string.IsNullOrEmpty(content))
        {
            sb.Append(content);
        }

        return sb.ToString();
    }
}