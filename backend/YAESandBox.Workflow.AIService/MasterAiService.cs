using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.AIService.ConfigManagement; // 用于缓存 IAiProcessor 实例 (可选)

namespace YAESandBox.Workflow.AIService;

/// <summary>
/// 虽然叫这个名字，但是它提供的是具有内部状态的 IAiProcessor，而非无状态的AI服务。
/// </summary>
public interface IMasterAiService
{
    public List<string>? GetAbleAiProcessorType(string aiProcessorConfigUuid);
    IAiProcessor? CreateAiProcessor(string aiProcessorConfigUuid, string aiModuleType);
}

public class MasterAiService(IHttpClientFactory httpClientFactory, IAiConfigurationProvider configProvider) : IMasterAiService
{
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    private IAiConfigurationProvider ConfigProvider { get; } = configProvider;

    public List<string>? GetAbleAiProcessorType(string aiProcessorConfigUuid) =>
        this.ConfigProvider.GetConfigurationSet(aiProcessorConfigUuid)?.GetAllDefinedTypes();

    public IAiProcessor? CreateAiProcessor(string aiProcessorConfigUuid, string aiModuleType)
    {
        if (string.IsNullOrEmpty(aiProcessorConfigUuid))
            return null;

        var configs = this.ConfigProvider.GetConfigurationSet(aiProcessorConfigUuid);

        if (configs == null) return null;

        // 调用配置对象的工厂方法
        var config = configs.FindAiConfig(aiModuleType);
        if (!config.TryGetValue(out var value))
            return null;

        var httpClient = this.HttpClientFactory.CreateClient(aiModuleType); // 使用配置的类型作为客户端名称
        var dependencies = new AiProcessorDependencies(httpClient);
        var specificService = value.ToAiProcessor(dependencies);
        // --- 变化结束 ---

        return specificService;
    }
}