using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.Attributes;

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
        Name = "AbstractAiProcessorConfig_MaxOutputTokens_Label",
        Description = "AbstractAiProcessorConfig_MaxOutputTokens_Description",
        ResourceType = typeof(AiProcessorConfigResources)
    )]
    [DefaultValue(8192)]
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// Apikey
    /// </summary>
    [Display(
        Name = "DoubaoAiProcessorConfig_ApiKey_Label",
        Description = "DoubaoAiProcessorConfig_ApiKey_Description",
        Prompt = "DoubaoAiProcessorConfig_ApiKey_Prompt",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    [Required(
        ErrorMessageResourceName = "Validation_Required",
        ErrorMessageResourceType = typeof(AiProcessorConfigResources) // 通用验证消息
    )]
    public string? ApiKey { get; init; }

    /// <summary>
    /// 模型名称
    /// </summary>
    [Display(
        Name = "DoubaoAiProcessorConfig_ModelName_Label",
        Description = "DoubaoAiProcessorConfig_ModelName_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    [Required(
        ErrorMessageResourceName = "Validation_Required",
        ErrorMessageResourceType = typeof(AiProcessorConfigResources)
    )]
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
        Name = "DoubaoAiProcessorConfig_Temperature_Label",
        Description = "DoubaoAiProcessorConfig_Temperature_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    [Range(0.0, 2.0,
        ErrorMessageResourceName = "Validation_Range",
        ErrorMessageResourceType = typeof(AiProcessorConfigResources)
    )]
    [DefaultValue(1.0)]
    public double? Temperature { get; init; }

    [Display(
        Name = "DoubaoAiProcessorConfig_TopP_Label",
        Description = "DoubaoAiProcessorConfig_TopP_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    [Range(0.0, 1.0,
        ErrorMessageResourceName = "Validation_Range",
        ErrorMessageResourceType = typeof(AiProcessorConfigResources)
    )]
    [DefaultValue(0.7)]
    public float? TopP { get; init; }

    [Display(
        Name = "DoubaoAiProcessorConfig_StopSequences_Label",
        Description = "DoubaoAiProcessorConfig_StopSequences_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    public IReadOnlyList<string>? StopSequences { get; init; }

    [Display(
        Name = "DoubaoAiProcessorConfig_ResponseFormatType_Label",
        Description = "DoubaoAiProcessorConfig_ResponseFormatType_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    [StringOptions("text", "json_object")]
    [DefaultValue("text")]
    public string? ResponseFormatType { get; init; }

    // 如果需要ResponseFormatType支持 json_schema，则需要更复杂的类型，例如:
    // public DoubaoResponseFormatJsonSchema? JsonSchemaResponseFormat { get; init; }

    [Display(
        Name = "DoubaoAiProcessorConfig_FrequencyPenalty_Label",
        Description = "DoubaoAiProcessorConfig_FrequencyPenalty_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    [Range(-2.0, 2.0,
        ErrorMessageResourceName = "Validation_Range",
        ErrorMessageResourceType = typeof(AiProcessorConfigResources)
    )]
    public float? FrequencyPenalty { get; init; }

    [Display(
        Name = "DoubaoAiProcessorConfig_PresencePenalty_Label",
        Description = "DoubaoAiProcessorConfig_PresencePenalty_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    [Range(-2.0, 2.0,
        ErrorMessageResourceName = "Validation_Range",
        ErrorMessageResourceType = typeof(AiProcessorConfigResources)
    )]
    public float? PresencePenalty { get; init; }


    [Display(
        Name = "DoubaoAiProcessorConfig_StreamOptions_IncludeUsage_Label",
        Description = "DoubaoAiProcessorConfig_StreamOptions_IncludeUsage_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    public bool? StreamOptionsIncludeUsage { get; init; }

    [Display(
        Name = "DoubaoAiProcessorConfig_ServiceTier_Label",
        Description = "DoubaoAiProcessorConfig_ServiceTier_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    [StringOptions("default", "auto")]
    public string? ServiceTier { get; init; }

    [Display(
        Name = "DoubaoAiProcessorConfig_Logprobs_Label",
        Description = "DoubaoAiProcessorConfig_Logprobs_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    public bool? Logprobs { get; init; }

    [Display(
        Name = "DoubaoAiProcessorConfig_TopLogprobs_Label",
        Description = "DoubaoAiProcessorConfig_TopLogprobs_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    [Range(0, 20,
        ErrorMessageResourceName = "Validation_Range",
        ErrorMessageResourceType = typeof(AiProcessorConfigResources)
    )]
    public int? TopLogprobs { get; init; }

    [Display(
        Name = "DoubaoAiProcessorConfig_LogitBias_Label",
        Description = "DoubaoAiProcessorConfig_LogitBias_Description",
        ResourceType = typeof(DoubaoConfigResources)
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