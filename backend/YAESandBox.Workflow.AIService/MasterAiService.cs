using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.AIService.ConfigManagement;

// 用于缓存 IAiProcessor 实例 (可选)

namespace YAESandBox.Workflow.AIService;

/// <summary>
/// 虽然叫这个名字，但是它提供的是具有内部状态的 IAiProcessor，而非无状态的AI服务。
/// </summary>
public interface IMasterAiService
{
    /// <summary>
    /// 创建一个 AI 处理器
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="aiProcessorConfigUuid"></param>
    /// <param name="aiModuleType"></param>
    /// <returns></returns>
    IAiProcessor? CreateAiProcessor(string userId, string aiProcessorConfigUuid, string aiModuleType);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    SubAiService ToSubAiService(string userId) => new(this, userId);
}

/// <summary>
/// 子AI服务，基于用户名
/// </summary>
/// <param name="MasterAiService"></param>
/// <param name="UserId"></param>
public record SubAiService(IMasterAiService MasterAiService, string UserId)
{
    /// <summary>
    /// 创建一个 AI 处理器
    /// </summary>
    /// <param name="aiProcessorConfigUuid"></param>
    /// <param name="aiModuleType"></param>
    /// <returns></returns>
    public IAiProcessor? CreateAiProcessor(string aiProcessorConfigUuid, string aiModuleType) =>
        this.MasterAiService.CreateAiProcessor(this.UserId, aiProcessorConfigUuid, aiModuleType);
}

/// <inheritdoc />
public class MasterAiService(IAiHttpClientFactory aiHttpClientFactory, IAiConfigurationProvider configProvider) : IMasterAiService
{
    private IAiHttpClientFactory HttpClientFactory { get; } = aiHttpClientFactory;
    private IAiConfigurationProvider ConfigProvider { get; } = configProvider;

    /// <inheritdoc />
    public IAiProcessor? CreateAiProcessor(string userId, string aiProcessorConfigUuid, string aiModuleType)
    {
        if (string.IsNullOrEmpty(aiProcessorConfigUuid))
            return null;

        var configs = this.ConfigProvider.GetConfigurationSet(userId, aiProcessorConfigUuid);

        if (configs == null) return null;

        // 调用配置对象的工厂方法
        var config = configs.FindAiConfig(aiModuleType);
        if (!config.TryGetValue(out var value))
            return null;

        var httpClient = this.HttpClientFactory.CreateClient(aiModuleType);
        var dependencies = new AiProcessorDependencies(httpClient);
        var specificService = value.ToAiProcessor(dependencies);

        return specificService;
    }
}