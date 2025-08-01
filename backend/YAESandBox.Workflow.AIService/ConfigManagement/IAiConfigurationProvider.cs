using YAESandBox.Workflow.AIService.AiConfig;

namespace YAESandBox.Workflow.AIService.ConfigManagement;

/// <summary>
/// 
/// </summary>
public interface IAiConfigurationProvider
{
    /// <summary>
    /// 根据配置键获取具体的 AI 配置对象。
    /// </summary>
    /// <param name="userId">配置所属用户的ID。</param>
    /// <param name="aiConfigKey">配置的唯一键，用于查找。</param>
    /// <returns>IAiProcessorConfig 实例，如果未找到则为 null。</returns>
    internal AiConfigurationSet? GetConfigurationSet(string userId, string aiConfigKey);
}