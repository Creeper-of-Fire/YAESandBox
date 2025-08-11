// 文件路径: YAESandBox.Workflow.AIService/AiConfig/DeepSeek/DeepSeekAiProcessorConfig.cs

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.AspNetCore.Secret;
using YAESandBox.Depend.AspNetCore.Secret.Mark;
using YAESandBox.Depend.Schema.SchemaProcessor;

namespace YAESandBox.Workflow.AIService.AiConfig.DeepSeek;

/// <summary>
/// DeepSeek的AI配置。
/// </summary>
internal record DeepSeekAiProcessorConfig() : AbstractAiProcessorConfig("DeepSeek")
{
    // --- 核心配置 ---

    [Required(ErrorMessage = "API密钥是必填项。")]
    [DataType(DataType.Password)]
    [Display(Name = "API 密钥 (API Key)", Description = "您的 DeepSeek API 密钥。")]
    [Protected]
    public string? ApiKey { get; init; }

    [Required(ErrorMessage = "模型名称是必填项。")]
    [DefaultValue("deepseek-chat")]
    [StringOptions("deepseek-chat", "deepseek-reasoner", IsEditableSelectOptions = true)]
    [Display(Name = "模型名称 (Model Name)", Description = "要使用的模型ID, 例如 'deepseek-chat' 或 'deepseek-reasoner'。")]
    public string? ModelName { get; init; }

    // --- 生成控制参数 ---
    [Display(Name = "最大输出Token数 (Max Tokens)", Description = "限制单次请求生成的最大Token数量。")]
    [DefaultValue(8192)]
    public int? MaxOutputTokens { get; init; }

    [Range(0.0, 2.0, ErrorMessage = "温度值必须在 0.0 到 2.0 之间。")]
    [DefaultValue(1.0)]
    [Display(Name = "温度 (Temperature)", Description = "控制输出的随机性。较低的值更确定，较高的值更随机。介于0和2之间。")]
    public double? Temperature { get; init; }

    [Range(0.0, 1.0, ErrorMessage = "Top P值必须在 0.0 到 1.0 之间。")]
    [DefaultValue(1.0)]
    [Display(Name = "Top P", Description = "核心采样参数，模型会考虑概率总和为 top_p 的token。不建议与温度同时修改。")]
    public float? TopP { get; init; }

    [Display(Name = "停止序列 (Stop Sequences)", Description = "一个或多个字符串，当模型生成这些字符串时将停止输出。最多16个。")]
    public IReadOnlyList<string>? StopSequences { get; init; }

    [Display(Name = "响应格式 (Response Format)", Description = "指定模型必须输出的格式。设置为 'json_object' 以启用 JSON 模式。")]
    [StringOptions("text", "json_object")]
    [DefaultValue("text")]
    public string? ResponseFormatType { get; init; }

    // --- 惩罚参数 ---

    [Range(-2.0, 2.0, ErrorMessage = "频率惩罚值必须在 -2.0 到 2.0 之间。")]
    [DefaultValue(0.0)]
    [Display(Name = "频率惩罚 (Frequency Penalty)", Description = "正值会根据token在已有文本中的频率来惩罚新token，降低重复内容的可能性。")]
    public float? FrequencyPenalty { get; init; }

    [Range(-2.0, 2.0, ErrorMessage = "存在惩罚值必须在 -2.0 到 2.0 之间。")]
    [DefaultValue(0.0)]
    [Display(Name = "存在惩罚 (Presence Penalty)", Description = "正值会根据token是否已在文本中出现来惩罚新token，鼓励模型谈论新主题。")]
    public float? PresencePenalty { get; init; }

    // --- 流式与高级选项 ---

    [Display(Name = "流式选项：包含用量 (Include Usage in Stream)", Description = "如果为true，在流式输出的末尾会额外发送一个包含token用量统计的数据块。")]
    public bool? StreamOptionsIncludeUsage { get; init; }

    [Display(Name = "返回对数概率 (Logprobs)", Description = "是否返回输出token的对数概率。")]
    public bool? Logprobs { get; init; }

    [Range(0, 20, ErrorMessage = "Top Logprobs 值必须在 0 到 20 之间。")]
    [Display(Name = "返回Top N对数概率 (Top Logprobs)", Description = "指定在每个位置返回概率最高的N个token及其对数概率。需要Logprobs为true。")]
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