using System.Collections.Concurrent; // 用于缓存 IAiProcessor 实例 (可选)

namespace YAESandBox.Workflow.AIService;

/// <summary>
/// 虽然叫这个名字，但是它提供的是具有内部状态的 IAiProcessor，而非无状态的AI服务。
/// </summary>
public interface IMasterAiService
{
    IAiProcessor? CreateAiProcessor(string aiProcessorConfigUUID);
}

public class MasterAiService(IHttpClientFactory httpClientFactory, IAiConfigurationProvider configProvider) : IMasterAiService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IAiConfigurationProvider _configProvider = configProvider;

    public IAiProcessor? CreateAiProcessor(string aiProcessorConfigUUID)
    {
        if (string.IsNullOrEmpty(aiProcessorConfigUUID)) return null;

        var config = this._configProvider.GetConfiguration(aiProcessorConfigUUID);

        if (config == null) return null;

        // --- 关键变化在这里 ---
        // 准备依赖项
        // 创建一个 HttpClient 实例。可以为不同的 ServiceIdentifier 类型创建不同配置的 Client。
        // 例如，如果 config.ServiceIdentifier 包含 "Doubao"，则创建一个为豆包优化的 HttpClient。
        // 这里简化为使用一个通用的或基于 config.ServiceIdentifier 命名的 client。
        var httpClient = this._httpClientFactory.CreateClient(config.UUID); // 使用配置的标识符作为客户端名称
        // 或者，如果所有 AI 服务共用一种 HttpClient 配置：
        // HttpClient httpClient = _httpClientFactory.CreateClient("DefaultAiHttpClient");

        var dependencies = new AiProcessorDependencies(httpClient);

        // 调用配置对象的工厂方法
        var specificService = config.ToAiProcessor(dependencies);
        // --- 变化结束 ---

        return specificService;
    }
}