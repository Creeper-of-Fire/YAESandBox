using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.AIService.ConfigManagement; // 用于缓存 IAiProcessor 实例 (可选)

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

        var httpClient = this._httpClientFactory.CreateClient(config.ConfigName); // 使用配置的标识名称作为客户端名称

        var dependencies = new AiProcessorDependencies(httpClient);

        // 调用配置对象的工厂方法
        var specificService = config.ToAiProcessor(dependencies);
        // --- 变化结束 ---

        return specificService;
    }
}