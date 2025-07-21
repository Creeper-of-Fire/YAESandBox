using System.Text.Json.Serialization;
using YAESandBox.Workflow.AIService.Shared;

namespace YAESandBox.Workflow.AIService.AiConfig.DeepSeek;

// --- 请求模型 ---
/// <summary>
/// 聊天消息
/// </summary>
/// <param name="Role"></param>
/// <param name="Content"></param>
/// <param name="Name"></param>
internal record DeepSeekChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")]
    string Content,
    [property: JsonPropertyName("name")] string? Name = null
);

internal record DeepSeekChatRequest(
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("messages")]
    IReadOnlyList<DeepSeekChatMessage> Messages,
    [property: JsonPropertyName("stream")] bool? Stream = null,
    [property: JsonPropertyName("stream_options")]
    object? StreamOptions = null, // 使用 object 以匹配 { "include_usage": ... } 结构
    [property: JsonPropertyName("temperature")]
    double? Temperature = null,
    [property: JsonPropertyName("max_tokens")]
    int? MaxTokens = null,
    [property: JsonPropertyName("top_p")] float? TopP = null,
    [property: JsonPropertyName("stop")] IReadOnlyList<string>? Stop = null,
    [property: JsonPropertyName("response_format")]
    object? ResponseFormat = null, // 使用 object 以匹配 { "type": "..." } 结构
    [property: JsonPropertyName("frequency_penalty")]
    float? FrequencyPenalty = null,
    [property: JsonPropertyName("presence_penalty")]
    float? PresencePenalty = null,
    [property: JsonPropertyName("logprobs")]
    bool? Logprobs = null,
    [property: JsonPropertyName("top_logprobs")]
    int? TopLogprobs = null
);

// --- 通用部分 ---
internal record DeepSeekChoiceBase
{
    [JsonPropertyName("index")] public int? Index { get; init; }
    [JsonPropertyName("finish_reason")] public string? FinishReason { get; init; }
}

internal record DeepSeekResponseMessage : IAiResponseMessagePart
{
    [JsonPropertyName("role")] public string? Role { get; init; }
    public string? GetContent() => this.Content;
    public string? GetReasoningContent() => this.ReasoningContent;

    [JsonPropertyName("content")] public string? Content { get; init; }

    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; init; }
}

// 不全，具体请参考官方文档吧
internal record DeepSeekUsage
{
    [JsonPropertyName("prompt_tokens")] public int? PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int? CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")] public int? TotalTokens { get; init; }
}

// --- 流式响应模型 ---
internal record DeepSeekStreamChoice : DeepSeekChoiceBase
{
    [JsonPropertyName("delta")] public DeepSeekResponseMessage? Delta { get; init; }
}

internal record DeepSeekStreamChunk
{
    [JsonPropertyName("id")] public string? Id { get; init; } = string.Empty;

    [JsonPropertyName("object")] public string? Object { get; init; } = string.Empty; // "chat.completion"

    [JsonPropertyName("created")] public long? Created { get; init; }

    [JsonPropertyName("model")] public string? Model { get; init; } = string.Empty;

    [JsonPropertyName("choices")] public IReadOnlyList<DeepSeekStreamChoice>? Choices { get; init; } = [];
}

// --- 非流式响应模型 ---
internal record DeepSeekCompletionChoice : DeepSeekChoiceBase
{
    [JsonPropertyName("message")] public DeepSeekResponseMessage? Message { get; init; }
}

internal record DeepSeekCompletionResponse
{
    [JsonPropertyName("id")] public string? Id { get; init; } = string.Empty;

    [JsonPropertyName("object")] public string? Object { get; init; } = string.Empty; // "chat.completion"

    [JsonPropertyName("created")] public long? Created { get; init; }

    [JsonPropertyName("model")] public string? Model { get; init; } = string.Empty;

    [JsonPropertyName("choices")] public IReadOnlyList<DeepSeekCompletionChoice> Choices { get; init; } = [];

    [JsonPropertyName("usage")] public DeepSeekUsage? Usage { get; init; }
}