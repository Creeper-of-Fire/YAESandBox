// 文件: IAiConfigurationManager.cs

using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService.AiConfig;

namespace YAESandBox.Workflow.AIService.ConfigManagement;

/// <summary>
/// 用于Upsert操作的返回结果，以区分是创建了新资源还是更新了现有资源。
/// </summary>
public enum UpsertResultType
{
    /// <summary>
    /// Created
    /// </summary>
    Created,
    /// <summary>
    /// Updated
    /// </summary>
    Updated
}

/// <summary>
/// 管理 AI 配置的持久化、加载、查询和新建操作。
/// 此接口也继承了 IAiConfigurationProvider，以便管理器可以直接用作配置提供者。
/// 所有操作都增加了 userId 参数以实现数据隔离。
/// </summary>
public interface IAiConfigurationManager : IAiConfigurationProvider
{
    /// <summary>
    /// 创建或更新一个 AI 配置集（Upsert）。
    /// 此方法是幂等的。如果具有指定UUID的配置已存在，则更新它；否则，创建它。
    /// </summary>
    /// <param name="userId">执行操作的用户ID。</param>
    /// <param name="uuid">要创建或更新的配置集的唯一标识符（由客户端提供）。</param>
    /// <param name="config">要保存的 AI 配置集对象。</param>
    /// <returns>操作结果。如果成功，返回一个指示操作是“创建”还是“更新”的枚举值。</returns>
    Task<Result<UpsertResultType>> UpsertConfigurationAsync(string userId, string uuid, AiConfigurationSet config);
    
    /// <summary>
    /// 根据 UUID 删除一个 AI 配置。
    /// </summary>
    /// <param name="userId">执行操作的用户ID。</param>
    /// <param name="uuid">要删除的配置的唯一标识符。</param>
    /// <returns>操作结果。如果成功或配置本就不存在，Result 通常为 Ok (幂等删除)；具体行为可由实现定义。</returns>
    Task<Result> DeleteConfigurationAsync(string userId, string uuid);

    /// <summary>
    /// 根据 UUID 获取一个 AI 配置。
    /// </summary>
    /// <param name="userId">执行操作的用户ID。</param>
    /// <param name="uuid">配置的唯一标识符。</param>
    /// <returns>一个 Result 对象，成功时包含 AI 配置；如果未找到，则 Result 失败并携带错误信息。</returns>
    Task<Result<AiConfigurationSet>> GetConfigurationByUuidAsync(string userId, string uuid);

    /// <summary>
    /// 获取指定用户的所有已存储的 AI 配置集。
    /// </summary>
    /// <param name="userId">执行操作的用户ID。</param>
    /// <returns>一个 Result 对象，成功时包含所有 AI 配置的列表。</returns>
    Task<Result<IReadOnlyDictionary<string, AiConfigurationSet>>> GetAllConfigurationsAsync(string userId);
}