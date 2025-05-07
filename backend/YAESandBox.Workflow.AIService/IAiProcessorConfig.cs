using System.Text.Json.Serialization;
using YAESandBox.Workflow.AIService.Doubao;

namespace YAESandBox.Workflow.AIService;

/// <summary>
/// 这个是内部新建Ai服务类时使用的东西。
/// </summary>
/// <param name="ConfigName">配置的名称，不唯一（防止不小心搞错了），建议保证其是唯一的</param>
/// <param name="ModuleType">模型的类型，持久化时工厂模式会使用它</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "ModuleType")]
[JsonDerivedType(typeof(DoubaoAiProcessorConfig), nameof(DoubaoAiProcessorConfig))] 
// 提示：如果将来添加了其他类型的配置，例如 MyOtherAiConfig，需要在这里也为它添加一个 [JsonDerivedType] 特性：
// [JsonDerivedType(typeof(MyOtherAiConfig), nameof(MyOtherAiConfig))]
public abstract record AbstractAiProcessorConfig(string ConfigName, string ModuleType)
{
    /// <summary>
    /// 根据此配置创建一个具体的 AI 处理器实例。
    /// </summary>
    /// <param name="dependencies">创建 AI 处理器所需的依赖项。</param>
    /// <returns>一个 IAiProcessor 实例。</returns>
    public abstract IAiProcessor ToAiProcessor(AiProcessorDependencies dependencies);
}

/// <summary>
/// 封装创建 IAiProcessor 时可能需要的共享依赖项
/// </summary>
/// <param name="HttpClient">用于创建 HttpClient 实例。每个 AI 服务需要特定配置的 HttpClient，MasterAiService 应该负责创建并传递。</param>
public record AiProcessorDependencies(HttpClient HttpClient);