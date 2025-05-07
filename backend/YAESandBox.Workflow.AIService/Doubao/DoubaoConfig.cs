namespace YAESandBox.Workflow.AIService.Doubao;

/// <summary>
/// 豆包的AI配置
/// </summary>
internal record DoubaoAiProcessorConfig(string ConfigName, string ApiKey, string ModelName)
    : AbstractAiProcessorConfig(ConfigName, nameof(DoubaoAiProcessorConfig))
{
    // public required string ApiKey { get; init; } = ApiKey;
    //
    // public required string ModelName { get; init; } = ModelName;

    // --- 以下是豆包 API 特有的请求参数 ---
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public float? TopP { get; init; }
    public IReadOnlyList<string>? StopSequences { get; init; }

    public string? ResponseFormatType { get; init; } // "text", "json_object"

    // 如果需要支持 json_schema，则需要更复杂的类型，例如:
    // public DoubaoResponseFormatJsonSchema? JsonSchemaResponseFormat { get; init; }
    public float? FrequencyPenalty { get; init; }
    public float? PresencePenalty { get; init; }
    public DoubaoStreamOptions? StreamOptions { get; init; }
    public string? ServiceTier { get; init; } // "auto" or "default"
    public bool? Logprobs { get; init; }
    public int? TopLogprobs { get; init; }
    public IReadOnlyDictionary<string, float>? LogitBias { get; init; }
    public IReadOnlyList<DoubaoTool>? Tools { get; init; }

    public override IAiProcessor ToAiProcessor(AiProcessorDependencies dependencies)
    {
        // 将整个配置对象自身传递给 DoubaoAiProcessor
        return new DoubaoAiProcessor(dependencies, this);
    }
}