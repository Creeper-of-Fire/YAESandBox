namespace YAESandBox.Workflow.AIService.Shared;

/// <summary>
/// 定义了一个AI响应片段提供其内容的能力。
/// 这个接口是实现响应格式化逻辑复用的关键，
/// 它允许我们用统一的方式处理来自不同AI模型的响应部分（如流式delta或非流式message）。
/// </summary>
public interface IAiResponseMessagePart
{
    /// <summary>
    /// 获取主要的文本内容。
    /// </summary>
    /// <returns>主要的回复文本，如果不存在则为null。</returns>
    string? GetContent();

    /// <summary>
    /// 获取思维链或推理过程的内容。
    /// </summary>
    /// <returns>思维链内容，如果模型不支持或本次未返回，则为null。</returns>
    string? GetReasoningContent();
}