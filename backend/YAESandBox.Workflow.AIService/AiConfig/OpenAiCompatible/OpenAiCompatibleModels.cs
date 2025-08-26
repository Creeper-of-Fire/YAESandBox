using System.Text.Json.Serialization;
using YAESandBox.Workflow.AIService.Shared;

namespace YAESandBox.Workflow.AIService.AiConfig.OpenAiCompatible;

// --- 请求体模型 ---
internal record OpenAiChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")]
    string Content,
    [property: JsonPropertyName("name")] string? Name = null
);

internal record OpenAiResponseFormat(
    [property: JsonPropertyName("type")] string? Type
);

internal record OpenAiChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")]
    IReadOnlyList<OpenAiChatMessage> Messages,
    [property: JsonPropertyName("stream")] bool? Stream = null,
    [property: JsonPropertyName("temperature")]
    double? Temperature = null,
    [property: JsonPropertyName("max_tokens")]
    int? MaxTokens = null,
    [property: JsonPropertyName("top_p")] float? TopP = null,
    [property: JsonPropertyName("stop")] IReadOnlyList<string>? Stop = null,
    [property: JsonPropertyName("response_format")]
    OpenAiResponseFormat? ResponseFormat = null
);

// --- 响应体模型 ---
internal record OpenAiResponseMessage : IAiResponseMessagePart
{
    [JsonPropertyName("role")] public string? Role { get; init; }
    [JsonPropertyName("content")] public string? Content { get; init; }

    public string? GetContent() => this.Content;
    public string? GetReasoningContent() => null; // 标准 OpenAI 格式没有 reasoning_content
}

internal record OpenAiChoiceBase
{
    [JsonPropertyName("index")] public int Index { get; init; }
    [JsonPropertyName("finish_reason")] public string? FinishReason { get; init; }
}

// 流式响应模型
internal record OpenAiStreamChoice : OpenAiChoiceBase
{
    [JsonPropertyName("delta")] public OpenAiResponseMessage Delta { get; init; } = new();
}

internal record OpenAiStreamChunk
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("object")] public string Object { get; init; } = string.Empty;
    [JsonPropertyName("created")] public long Created { get; init; }
    [JsonPropertyName("model")] public string Model { get; init; } = string.Empty;
    [JsonPropertyName("choices")] public IReadOnlyList<OpenAiStreamChoice> Choices { get; init; } = [];
}

// 非流式响应模型
internal record OpenAiCompletionChoice : OpenAiChoiceBase
{
    [JsonPropertyName("message")] public OpenAiResponseMessage Message { get; init; } = new();
}

internal record OpenAiUsage
{
    [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")] public int TotalTokens { get; init; }
}

internal record OpenAiCompletionResponse
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("object")] public string Object { get; init; } = string.Empty;
    [JsonPropertyName("created")] public long Created { get; init; }
    [JsonPropertyName("model")] public string Model { get; init; } = string.Empty;
    [JsonPropertyName("choices")] public IReadOnlyList<OpenAiCompletionChoice> Choices { get; init; } = [];
    [JsonPropertyName("usage")] public OpenAiUsage? Usage { get; init; }
}