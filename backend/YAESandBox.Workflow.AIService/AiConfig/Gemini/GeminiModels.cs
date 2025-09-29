using System.Text.Json.Serialization;
using YAESandBox.Workflow.AIService.Shared;

namespace YAESandBox.Workflow.AIService.AiConfig.Gemini;

// --- 通用模型 ---

internal record GeminiPart
{
    [JsonPropertyName("text")] public string? Text { get; init; }
}

internal record GeminiContent
{
    [JsonPropertyName("role")] public string? Role { get; init; }

    [JsonPropertyName("parts")] public IReadOnlyList<GeminiPart>? Parts { get; init; }
}

internal record GeminiSafetyRating
{
    [JsonPropertyName("category")] public string? Category { get; init; }

    [JsonPropertyName("probability")] public string? Probability { get; init; }
}

internal record GeminiCandidate
{
    [JsonPropertyName("content")] public GeminiContent? Content { get; init; }

    [JsonPropertyName("finishReason")] public string? FinishReason { get; init; }

    [JsonPropertyName("safetyRatings")] public IReadOnlyList<GeminiSafetyRating>? SafetyRatings { get; init; }
}

// --- 请求体模型 ---

internal record GeminiGenerationConfig
{
    [JsonPropertyName("temperature")] public double? Temperature { get; init; }

    [JsonPropertyName("topP")] public float? TopP { get; init; }

    [JsonPropertyName("topK")] public int? TopK { get; init; }

    [JsonPropertyName("maxOutputTokens")] public int? MaxOutputTokens { get; init; }

    [JsonPropertyName("stopSequences")] public IReadOnlyList<string>? StopSequences { get; init; }

    [JsonPropertyName("responseMimeType")] public string? ResponseMimeType { get; init; }
}

internal record GeminiGenerateContentRequest
{
    [JsonPropertyName("contents")] public IReadOnlyList<GeminiContent> Contents { get; init; } = [];

    [JsonPropertyName("systemInstruction")]
    public GeminiContent? SystemInstruction { get; init; }

    [JsonPropertyName("generationConfig")] public GeminiGenerationConfig? GenerationConfig { get; init; }

    // 其他字段如 tools, toolConfig, safetySettings 可以根据需要添加
}

// --- 响应体模型 ---

// Gemini 的流式和非流式响应结构非常相似，主要区别在于流式是分块发送的。
// 我们可以用一个基类来统一处理响应内容。
// 为了与你现有的 AiResponseFormatter 兼容，我们让响应体实现 IAiResponseMessagePart。

internal record GeminiGenerateContentResponse : IAiResponseMessagePart
{
    [JsonPropertyName("candidates")] public IReadOnlyList<GeminiCandidate>? Candidates { get; init; }

    // 为 AiResponseFormatter 提供统一的内容获取方法
    public string? GetContent() => this.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

    // Gemini 没有独立的 reasoning_content 字段，这里返回 null
    public string? GetReasoningContent() => null;
}