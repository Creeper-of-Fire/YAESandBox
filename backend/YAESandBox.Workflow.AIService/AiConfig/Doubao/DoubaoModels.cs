using System.Text.Json.Serialization;
using YAESandBox.Workflow.AIService.Shared;

namespace YAESandBox.Workflow.AIService.AiConfig.Doubao;

// --- 请求体模型 ---
/// <summary>
/// 聊天消息
/// </summary>
/// <param name="Role"></param>
/// <param name="Content"></param>
/// <param name="Name"></param>
internal record DoubaoChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")]
    string Content,
    [property: JsonPropertyName("name")] string? Name = null
);

/// <summary>
/// 流式配置选项
/// </summary>
/// <param name="IncludeUsage"></param>
internal record DoubaoStreamOptions(
    [property: JsonPropertyName("include_usage")]
    bool? IncludeUsage = null
);

/// <summary>
/// 工具定义 (请求时可能用到)
/// </summary>
internal record DoubaoTool(
    [property: JsonPropertyName("type")] string Type, // "function"
    [property: JsonPropertyName("function")]
    DoubaoFunctionDefinition Function
);

/// <summary>
/// 工具的函数定义 (请求时可能用到)
/// </summary>
internal record DoubaoFunctionDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")]
    string Description,
    [property: JsonPropertyName("parameters")]
    object Parameters // JSON Schema 对象
);

/// <summary>
/// 回应体的回应格式
/// </summary>
internal record DoubaoResponseFormat(string? Type = null)
{
    /// <summary>
    /// 返回类型，默认为text
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; private init; } = Type ?? "text";
}

internal record DoubaoChatRequest(
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("messages")]
    IReadOnlyList<DoubaoChatMessage> Messages,
    [property: JsonPropertyName("stream")] bool? Stream = null,
    [property: JsonPropertyName("stream_options")]
    DoubaoStreamOptions? StreamOptions = null,
    
    [property: JsonPropertyName("temperature")]
    double? Temperature = null,
    [property: JsonPropertyName("max_tokens")]
    int? MaxTokens = null,
    [property: JsonPropertyName("top_p")] float? TopP = null,
    [property: JsonPropertyName("stop")] IReadOnlyList<string>? Stop = null,
    [property: JsonPropertyName("response_format")]
    DoubaoResponseFormat? ResponseFormat = null,
    [property: JsonPropertyName("frequency_penalty")]
    float? FrequencyPenalty = null,
    [property: JsonPropertyName("presence_penalty")]
    float? PresencePenalty = null,
    [property: JsonPropertyName("service_tier")]
    string? ServiceTier = null,
    [property: JsonPropertyName("logprobs")]
    bool? Logprobs = null,
    [property: JsonPropertyName("top_logprobs")]
    int? TopLogprobs = null,
    [property: JsonPropertyName("logit_bias")]
    IReadOnlyDictionary<string, float>? LogitBias = null, // 改回 IReadOnlyDictionary
    [property: JsonPropertyName("tools")] IReadOnlyList<DoubaoTool>? Tools = null // 改回 IReadOnlyList
);

// --- 响应体模型 (流式和非流式共用部分) ---
internal record DoubaoChoiceBase
{
    [JsonPropertyName("index")] public int? Index { get; init; }
    [JsonPropertyName("finish_reason")] public string? FinishReason { get; init; }
}

internal record DoubaoResponseMessage : IAiResponseMessagePart
{
    [JsonPropertyName("role")] public string? Role { get; init; } = string.Empty;
    public string? GetContent() => this.Content;
    public string? GetReasoningContent() => this.ReasoningContent;

    [JsonPropertyName("content")] public string? Content { get; init; } = string.Empty;

    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; init; }
}

internal record DoubaoUsage
{
    [JsonPropertyName("prompt_tokens")] public int? PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int? CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")] public int? TotalTokens { get; init; }

    [JsonPropertyName("prompt_tokens_details")]
    public DoubaoUsageDetails? PromptTokensDetails { get; init; }

    [JsonPropertyName("completion_tokens_details")]
    public DoubaoUsageDetails? CompletionTokensDetails { get; init; }
}

internal record DoubaoUsageDetails
{
    [JsonPropertyName("cached_tokens")] public int? CachedTokens { get; init; }
    [JsonPropertyName("reasoning_tokens")] public int? ReasoningTokens { get; init; }
}


// --- 流式响应模型 ---
internal record DoubaoStreamChoice : DoubaoChoiceBase
{
    [JsonPropertyName("delta")] public DoubaoResponseMessage? Delta { get; init; } = new();
}

internal record DoubaoStreamChunk
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;

    [JsonPropertyName("object")] public string Object { get; init; } = string.Empty; // "chat.completion.chunk"

    [JsonPropertyName("created")] public long Created { get; init; }

    [JsonPropertyName("model")] public string Model { get; init; } = string.Empty;

    [JsonPropertyName("choices")] public IReadOnlyList<DoubaoStreamChoice> Choices { get; init; } = [];

    [JsonPropertyName("usage")] // 流式时通常为 null
    public DoubaoUsage? Usage { get; init; }
}


// --- 非流式响应模型 ---

internal record DoubaoCompletionChoice : DoubaoChoiceBase
{
    [JsonPropertyName("message")] public DoubaoResponseMessage? Message { get; init; } = new();
    
    [JsonPropertyName("logprobs")] public object? Logprobs { get; init; } // 通常为 null
}


internal record DoubaoCompletionResponse
{
    [JsonPropertyName("id")] public string? Id { get; init; } = string.Empty;

    [JsonPropertyName("object")] public string? Object { get; init; } = string.Empty; // "chat.completion"

    [JsonPropertyName("created")] public long? Created { get; init; }

    [JsonPropertyName("model")] public string? Model { get; init; } = string.Empty;

    [JsonPropertyName("choices")] public IReadOnlyList<DoubaoCompletionChoice>? Choices { get; init; } = [];

    [JsonPropertyName("usage")] public DoubaoUsage? Usage { get; init; }
}