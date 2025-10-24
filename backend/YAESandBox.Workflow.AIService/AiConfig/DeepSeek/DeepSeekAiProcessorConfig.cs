using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Depend.Secret.Mark;

namespace YAESandBox.Workflow.AIService.AiConfig.DeepSeek;

/// <summary>
/// DeepSeek的AI配置。
/// </summary>
internal record DeepSeekAiProcessorConfig() : AbstractAiProcessorConfig("DeepSeek")
{
    [Display(Name = "最大输出Token数 (Max Tokens)", Description = "限制单次请求生成的最大Token数量。")]
    [DefaultValue(8192)]
    public int? MaxOutputTokens { get; init; }

    // --- 核心配置 ---

    [Display(
        Name = "GeneralAiConfig_ApiKey_Label",
        Description = "GeneralAiConfig_ApiKey_Description",
        Prompt = "GeneralAiConfig_ApiKey_Prompt",
        ResourceType = typeof(GeneralAiResources)
    )]
    [Required]
    [DataType(DataType.Password)]
    [Protected]
    public string? ApiKey { get; init; }

    /// <summary>
    /// 模型名称
    /// </summary>
    [Display(
        Name = "GeneralAiConfig_ModelName_Label",
        Description = "GeneralAiConfig_ModelName_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [Required]
    [DefaultValue("deepseek-chat")]
    [StringOptions("deepseek-chat", "deepseek-reasoner", IsEditableSelectOptions = true)]
    public string? ModelName { get; init; }

    // --- 生成控制参数 ---

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
        Name = "DeepSeekAiConfig_StopSequences_Label",
        Description = "DeepSeekAiConfig_StopSequences_Description",
        ResourceType = typeof(DeepSeekAiResources)
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

    // --- 惩罚参数 ---

    [Display(
        Name = "GeneralAiConfig_FrequencyPenalty_Label",
        Description = "GeneralAiConfig_FrequencyPenalty_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [Range(-2.0, 2.0)]
    [DefaultValue(0.0)]
    public float? FrequencyPenalty { get; init; }

    [Display(
        Name = "GeneralAiConfig_PresencePenalty_Label",
        Description = "GeneralAiConfig_PresencePenalty_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [Range(-2.0, 2.0)]
    [DefaultValue(0.0)]
    public float? PresencePenalty { get; init; }

    // --- 流式与高级选项 ---

    [Display(
        Name = "GeneralAiConfig_StreamOptions_IncludeUsage_Label",
        Description = "GeneralAiConfig_StreamOptions_IncludeUsage_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    public bool? StreamOptionsIncludeUsage { get; init; }

    [Display(Name = "返回对数概率 (Logprobs)", Description = "是否返回输出token的对数概率。")]
    public bool? Logprobs { get; init; }

    [Display(
        Name = "GeneralAiConfig_Logprobs_Label",
        Description = "GeneralAiConfig_Logprobs_Description",
        ResourceType = typeof(GeneralAiResources)
    )]
    [Range(0, 20)]
    public int? TopLogprobs { get; init; }

    // --- 重写 ToAiProcessor 方法 ---

    /// <summary>
    /// 根据此配置创建一个具体的 DeepSeek AI 处理器实例。
    /// </summary>
    /// <param name="dependencies">创建 AI 处理器所需的依赖项，如 HttpClient。</param>
    /// <returns>一个配置好的 IAiProcessor 实例。</returns>
    public override IAiProcessor ToAiProcessor(AiProcessorDependencies dependencies)
    {
        return new DeepSeekAiProcessor(dependencies, this);
    }
}