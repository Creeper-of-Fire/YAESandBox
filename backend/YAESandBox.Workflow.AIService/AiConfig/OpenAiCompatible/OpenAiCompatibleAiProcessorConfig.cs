using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.AspNetCore.Secret.Mark;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.AIService.AiConfig.Doubao;

namespace YAESandBox.Workflow.AIService.AiConfig.OpenAiCompatible;

/// <summary>
/// 适用于任何兼容 OpenAI API 的通用 AI 配置。
/// </summary>
internal record OpenAiCompatibleAiProcessorConfig() : AbstractAiProcessorConfig(nameof(OpenAiCompatibleAiProcessorConfig))
{
    /// <summary>
    /// API 的基地址 (Base URL)。
    /// 例如: "https://api.openai.com/v1/" 或 "https://your-custom-endpoint/v1/"
    /// </summary>
    [Display(
        Name = "OpenAiCompatible_BaseUrl_Label",
        Description = "OpenAiCompatible_BaseUrl_Description",
        Prompt = "OpenAiCompatible_BaseUrl_Prompt",
        ResourceType = typeof(OpenAiCompatibleAiResources)
    )]
    [Required]
    [DataType(DataType.Url)]
    public string BaseUrl { get; init; } = "https://api.openai.com/v1/";

    /// <summary>
    /// API 密钥 (API Key)
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
    [DefaultValue("gpt-4o")]
    public string ModelName { get; init; } = "gpt-4o";

    /// <summary>
    /// 最大输出Token数
    /// </summary>
    [Display(
        Name = "GeneralAiConfig_MaxOutputTokens_Label",
        Description = "GeneralAiConfig_MaxOutputTokens_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    public int? MaxOutputTokens { get; init; }

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
    [DefaultValue(1.0)]
    public float? TopP { get; init; }

    [Display(
        Name = "DoubaoAiConfig_StopSequences_Label", // 可以复用或创建新的资源字符串
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

    public override IAiProcessor ToAiProcessor(AiProcessorDependencies dependencies)
    {
        // 将此配置对象传递给新的通用处理器
        return new OpenAiCompatibleAiProcessor(dependencies, this);
    }
}