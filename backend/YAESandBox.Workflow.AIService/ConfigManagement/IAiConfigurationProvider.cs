using YAESandBox.Workflow.AIService.AiConfig;

namespace YAESandBox.Workflow.AIService.ConfigManagement;

public interface IAiConfigurationProvider
{
    /// <summary>
    /// 根据配置键获取具体的 AI 配置对象。
    /// </summary>
    /// <param name="aiConfigKey">配置的唯一键，用于查找。</param>
    /// <returns>IAiProcessorConfig 实例，如果未找到则为 null。</returns>
    internal AbstractAiProcessorConfig? GetConfiguration(string aiConfigKey);
}