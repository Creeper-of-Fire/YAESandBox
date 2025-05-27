using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.Attributes;

namespace YAESandBox.Workflow.AIService.AiConfig;

/// <summary>
/// 这个是Ai服务配置的基类，仅含绝对存在的字段。
/// </summary>
[JsonConverter(typeof(AbstractAiProcessorConfigConverter))]
public abstract record AbstractAiProcessorConfig(string ConfigType)
{
    /// <summary>
    /// 根据此配置创建一个具体的 AI 处理器实例。
    /// </summary>
    /// <param name="dependencies">创建 AI 处理器所需的依赖项。</param>
    /// <returns>一个 IAiProcessor 实例。</returns>
    public abstract IAiProcessor ToAiProcessor(AiProcessorDependencies dependencies);

    /// <summary>
    /// 最大输入Token数。不出现在请求体中，但是在其他地方（如历史记录生成）会有用。
    /// </summary>
    [Display(
        Name = "AbstractAiProcessorConfig_MaxInputTokens_Label",
        Description = "AbstractAiProcessorConfig_MaxInputTokens_Description",
        ResourceType = typeof(AiProcessorConfigResources)
    )]
    [DefaultValue(int.MaxValue)]
    public int? MaxInputTokens { get; init; }

    /// <summary>
    /// AI配置的类型。
    /// </summary>
    [ReadOnly(true)]
    [Required]
    [HiddenInSchema(true)]
    public string ConfigType { get; init; } = ConfigType;

    // 没有流式响应的配置，因为流式应该在调用AI处配置，如工作流的AI配置。
}

/// <summary>
/// 封装创建 IAiProcessor 时可能需要的共享依赖项
/// </summary>
/// <param name="HttpClient">用于创建 HttpClient 实例。每个 AI 服务需要特定配置的 HttpClient，MasterAiService 应该负责创建并传递。</param>
public record AiProcessorDependencies(HttpClient HttpClient);