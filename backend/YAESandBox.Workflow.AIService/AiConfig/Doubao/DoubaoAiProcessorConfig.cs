using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.AspNetCore.Secret.Mark;
using YAESandBox.Depend.Schema.SchemaProcessor;

namespace YAESandBox.Workflow.AIService.AiConfig.Doubao;

/// <summary>
/// 豆包的AI配置
/// </summary>
internal record DoubaoAiProcessorConfig() : AbstractAiProcessorConfig(nameof(DoubaoAiProcessorConfig))
{
    /// <summary>
    /// 最大输出Token数
    /// </summary>
    [Display(
        Name = "GeneralAiConfig_MaxOutputTokens_Label",
        Description = "GeneralAiConfig_MaxOutputTokens_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [DefaultValue(8192)]
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// Apikey
    /// </summary>
    [Display(
        Name = "GeneralAiConfig_ApiKey_Label",
        Description = "GeneralAiConfig_ApiKey_Description",
        Prompt = "GeneralAiConfig_ApiKey_Prompt",
        ResourceType = typeof(GeneralAiResources)
    )]
    [DataType(DataType.Password)]
    [Required]
    [Protected]
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// 模型名称
    /// </summary>
    [Display(
        Name = "GeneralAiConfig_ModelName_Label",
        Description = "GeneralAiConfig_ModelName_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [Required]
    [StringOptions(
        "doubao-1-5-vision-pro-32k-250115",
        "doubao-1-5-lite-32k-250115",
        "doubao-1-5-pro-32k-250115",
        "doubao-1-5-pro-32k-character-250228",
        "doubao-1.5-thinking-pro-250415",
        "doubao-1.5-pro-256k-250115",
        "deepseek-r1-250120",
        IsEditableSelectOptions = true)]
    [DefaultValue("doubao-1-5-lite-32k-250115")]
    public string? ModelName { get; init; }

    [Display(
        Name = "GeneralAiConfig_Temperature_Label",
        Description = "GeneralAiConfig_Temperature_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [Range(0.0, 2.0)]
    [DefaultValue(1.0)]
    public double? Temperature { get; init; }

    [Display(
        Name = "GeneralAiConfig_TopP_Label",
        Description = "GeneralAiConfig_TopP_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [Range(0.0, 1.0)]
    [DefaultValue(0.7)]
    public float? TopP { get; init; }

    [Display(
        Name = "DoubaoAiConfig_StopSequences_Label",
        Description = "DoubaoAiConfig_StopSequences_Description",
        ResourceType = typeof(DoubaoAiResources)
    )]
    public IReadOnlyList<string>? StopSequences { get; init; }

    [Display(
        Name = "GeneralAiConfig_ResponseFormatType_Label",
        Description = "GeneralAiConfig_ResponseFormatType_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [StringOptions("text", "json_object")]
    [DefaultValue("text")]
    public string? ResponseFormatType { get; init; }

    // 如果需要ResponseFormatType支持 json_schema，则需要更复杂的类型，例如:
    // public DoubaoResponseFormatJsonSchema? JsonSchemaResponseFormat { get; init; }

    [Display(
        Name = "GeneralAiConfig_FrequencyPenalty_Label",
        Description = "GeneralAiConfig_FrequencyPenalty_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [Range(-2.0, 2.0)]
    public float? FrequencyPenalty { get; init; }

    [Display(
        Name = "GeneralAiConfig_PresencePenalty_Label",
        Description = "GeneralAiConfig_PresencePenalty_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [Range(-2.0, 2.0)]
    public float? PresencePenalty { get; init; }


    [Display(
        Name = "GeneralAiConfig_StreamOptions_IncludeUsage_Label",
        Description = "GeneralAiConfig_StreamOptions_IncludeUsage_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    public bool? StreamOptionsIncludeUsage { get; init; }

    [Display(
        Name = "DoubaoAiConfig_ServiceTier_Label",
        Description = "DoubaoAiConfig_ServiceTier_Description",
        ResourceType = typeof(DoubaoAiResources)
    )]
    [StringOptions("default", "auto")]
    public string? ServiceTier { get; init; }

    [Display(
        Name = "GeneralAiConfig_Logprobs_Label",
        Description = "GeneralAiConfig_Logprobs_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    public bool? Logprobs { get; init; }

    [Display(
        Name = "GeneralAiConfig_TopLogprobs_Label",
        Description = "GeneralAiConfig_TopLogprobs_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [Range(0, 20)]
    public int? TopLogprobs { get; init; }

    [Display(
        Name = "GeneralAiConfig_LogitBias_Label",
        Description = "GeneralAiConfig_LogitBias_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    public IReadOnlyList<LogitBiasItemDto>? LogitBias { get; init; }

    public record LogitBiasItemDto
    {
        [JsonPropertyName("tokenId")]
        [Required]
        public string TokenId { get; set; } = string.Empty;

        [JsonPropertyName("biasValue")]
        [Range(-100, 100)]
        [Required]
        public float BiasValue { get; set; }
    }

    public override IAiProcessor ToAiProcessor(AiProcessorDependencies dependencies)
    {
        // 将整个配置对象自身传递给 DoubaoAiProcessor
        return new DoubaoAiProcessor(dependencies, this);
    }
}