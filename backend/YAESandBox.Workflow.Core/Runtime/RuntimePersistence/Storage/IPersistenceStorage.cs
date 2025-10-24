using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.Core.Runtime.RuntimePersistence.Storage;

/// <summary>
/// 为 WorkflowPersistenceService 提供底层状态存储的抽象接口。
/// 这是一个简单的、基于实例ID的键值存储。
/// </summary>
public interface IPersistenceStorage
{
    /// <summary>
    /// 异步获取指定实例ID的状态记录。
    /// </summary>
    /// <param name="instanceId">实例的唯一ID。</param>
    /// <returns>一个 Result 对象，成功时包含找到的状态记录 (可能为 null)，失败时包含错误。</returns>
    Task<Result<InstanceStateRecord?>> GetStateAsync(Guid instanceId);

    /// <summary>
    /// 异步保存或更新指定实例ID的状态记录。
    /// </summary>
    /// <param name="stateRecord">要保存的状态记录。</param>
    /// <returns>一个表示操作结果的 Result。</returns>
    Task<Result> SetStateAsync(InstanceStateRecord stateRecord);

    /// <summary>
    /// 提供对所有当前实例状态的只读访问，方便外部（如监控UI）查询。
    /// </summary>
    /// <returns></returns>
    Task<Result<IReadOnlyDictionary<Guid, InstanceStateRecord>>> GetAllStatesAsync();
}