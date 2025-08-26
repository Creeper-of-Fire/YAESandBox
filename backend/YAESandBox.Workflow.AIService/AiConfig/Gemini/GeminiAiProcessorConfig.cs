using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.AspNetCore.Secret.Mark;
using YAESandBox.Depend.Schema.SchemaProcessor;

namespace YAESandBox.Workflow.AIService.AiConfig.Gemini;

/// <summary>
/// 谷歌 Gemini 的AI配置
/// </summary>
internal record GeminiAiProcessorConfig() : AbstractAiProcessorConfig(nameof(GeminiAiProcessorConfig))
{
    /// <summary>
    /// API Key
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
        "gemini-1.5-pro-latest",
        "gemini-1.5-flash-latest",
        "gemini-1.0-pro",
        "gemini-pro-vision", // 旧版多模态
        IsEditableSelectOptions = true)]
    [DefaultValue("gemini-1.5-flash-latest")]
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
    public float? TopP { get; init; }

    [Display(
        Name = "GeneralAiConfig_TopK_Label",
        Description = "GeneralAiConfig_TopK_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    public int? TopK { get; init; }

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

    [Display(
        Name = "GeneralAiConfig_StopSequences_Label",
        Description = "GeneralAiConfig_StopSequences_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    public IReadOnlyList<string>? StopSequences { get; init; }

    [Display(
        Name = "GeminiAiConfig_ResponseMimeType_Label",
        Description = "GeminiAiConfig_ResponseMimeType_Description",
        ResourceType = typeof(GeminiAiResources)
    )]
    [StringOptions("text/plain", "application/json")]
    [DefaultValue("text/plain")]
    public string? ResponseMimeType { get; init; }


    public override IAiProcessor ToAiProcessor(AiProcessorDependencies dependencies)
    {
        return new GeminiAiProcessor(dependencies, this);
    }
}