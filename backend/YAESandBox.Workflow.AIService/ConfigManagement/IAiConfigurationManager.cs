// 文件: IAiConfigurationManager.cs

using FluentResults;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService.AiConfig;

// 使用 Task 但用户不要求 CancellationToken

namespace YAESandBox.Workflow.AIService.ConfigManagement;

/// <summary>
/// 管理 AI 配置的持久化、加载、查询和新建操作。
/// 此接口也继承了 IAiConfigurationProvider，以便管理器可以直接用作配置提供者。
/// </summary>
public interface IAiConfigurationManager : IAiConfigurationProvider
{
    /// <summary>
    /// 添加一个新的 AI 配置。
    /// </summary>
    /// <param name="config">要添加的配置对象。</param>
    /// <returns>操作结果。如果成功，返回新的配置的UUID。</returns>
    Task<Result<string>> AddConfigurationAsync(AiConfigurationSet config);

    /// <summary>
    /// 更新一个已存在的 AI 配置。
    /// </summary>
    /// <param name="uuid">UUID 用于查找并替换现有配置。</param>
    /// <param name="config">包含更新信息的配置对象。</param>
    /// <returns>操作结果。如果成功，Result 为 Ok；如果具有该 UUID 的配置未找到，则返回错误。</returns>
    Task<Result> UpdateConfigurationAsync(string uuid, AiConfigurationSet config);

    /// <summary>
    /// 根据 UUID 删除一个 AI 配置。
    /// </summary>
    /// <param name="uuid">要删除的配置的唯一标识符。</param>
    /// <returns>操作结果。如果成功或配置本就不存在，Result 通常为 Ok (幂等删除)；具体行为可由实现定义。</returns>
    Task<Result> DeleteConfigurationAsync(string uuid);

    /// <summary>
    /// 根据 UUID 获取一个 AI 配置。
    /// </summary>
    /// <param name="uuid">配置的唯一标识符。</param>
    /// <returns>一个 Result 对象，成功时包含 AI 配置；如果未找到，则 Result 失败并携带错误信息。</returns>
    Task<Result<AiConfigurationSet>> GetConfigurationByUuidAsync(string uuid);

    /// <summary>
    /// 获取所有已存储的 AI 配置集。
    /// </summary>
    /// <returns>一个 Result 对象，成功时包含所有 AI 配置的列表。</returns>
    Task<Result<IReadOnlyDictionary<string, AiConfigurationSet>>> GetAllConfigurationsAsync();
}