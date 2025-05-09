using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Workflow.AIService.AiConfigSchema;

namespace YAESandBox.Workflow.AIService.AiConfig.Doubao;

/// <summary>
/// 豆包的AI配置
/// </summary>
public record DoubaoAiProcessorConfig(string ConfigName, string ApiKey, string ModelName)
    : AbstractAiProcessorConfig(ConfigName, nameof(DoubaoAiProcessorConfig))
{
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
    [DataType(DataType.Password)]
    public string ApiKey { get; init; } = ApiKey;

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
    [SelectOptions(
        "doubao-1-5-vision-pro-32k-250115",
        "doubao-1-5-lite-32k-250115",
        "doubao-1-5-pro-32k-250115",
        "doubao-1-5-pro-32k-character-250228",
        IsEditableSelectOptions = true)]
    [DefaultValue("doubao-1-5-lite-32k-250115")]
    public string ModelName { get; init; } = ModelName;

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
        Name = "DoubaoAiProcessorConfig_MaxTokens_Label",
        Description = "DoubaoAiProcessorConfig_MaxTokens_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    public int? MaxTokens { get; init; }

    [Display(
        Name = "DoubaoAiProcessorConfig_TopP_Label",
        Description = "DoubaoAiProcessorConfig_TopP_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    [Range(0.0, 1.0,
        ErrorMessageResourceName = "Validation_Range",
        ErrorMessageResourceType = typeof(AiProcessorConfigResources)
    )]
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
    [SelectOptions("text", "json_object")]
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
    public bool? StreamOptions_IncludeUsage { get; init; }

    [Display(
        Name = "DoubaoAiProcessorConfig_ServiceTier_Label",
        Description = "DoubaoAiProcessorConfig_ServiceTier_Description",
        ResourceType = typeof(DoubaoConfigResources)
    )]
    [SelectOptions("default", "auto")]
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
    public IReadOnlyDictionary<string, float>? LogitBias { get; init; }

    public override IAiProcessor ToAiProcessor(AiProcessorDependencies dependencies)
    {
        // 将整个配置对象自身传递给 DoubaoAiProcessor
        return new DoubaoAiProcessor(dependencies, this);
    }
}