using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.AIService.ConfigManagement; // 用于缓存 IAiProcessor 实例 (可选)

namespace YAESandBox.Workflow.AIService;

/// <summary>
/// 虽然叫这个名字，但是它提供的是具有内部状态的 IAiProcessor，而非无状态的AI服务。
/// </summary>
public interface IMasterAiService
{
    /// <summary>
    /// 创建一个 AI 处理器
    /// </summary>
    /// <param name="aiProcessorConfig"></param>
    /// <returns></returns>
    IAiProcessor CreateAiProcessor(AbstractAiProcessorConfig aiProcessorConfig);
}

/// <summary>
/// 主AI服务
/// </summary>
/// <param name="httpClientFactory">HTTP客户端工厂</param>
public class MasterAiService(IHttpClientFactory httpClientFactory) : IMasterAiService
{
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;

    /// <summary>
    /// 创建一个 AI 处理器
    /// </summary>
    /// <param name="aiProcessorConfig"></param>
    /// <returns></returns>
    public IAiProcessor CreateAiProcessor(AbstractAiProcessorConfig aiProcessorConfig)
    {
        // 调用配置对象的工厂方法
        var httpClient = this.HttpClientFactory.CreateClient(aiProcessorConfig.ConfigType); // 使用配置的类型作为客户端名称
        var dependencies = new AiProcessorDependencies(httpClient);
        var specificService = aiProcessorConfig.ToAiProcessor(dependencies);

        return specificService;
    }
}