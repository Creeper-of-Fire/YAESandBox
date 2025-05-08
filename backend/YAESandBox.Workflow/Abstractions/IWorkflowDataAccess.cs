// --- File: YAESandBox.Workflow/Abstractions/IWorkflowDataAccess.cs ---

using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;

namespace YAESandBox.Workflow.Abstractions;

/// <summary>
/// 定义工作流脚本访问游戏只读数据的接口。
/// 实现者通常会调用 BlockReadService 或类似服务。
/// </summary>
public interface IWorkflowDataAccess
{
    /// <summary>
    /// 获取指定 Block 的当前 GameState。
    /// </summary>
    /// <param name="blockId">目标 Block 的 ID。</param>
    /// <returns>GameState 实例，如果 Block 不存在则返回 null。</returns>
    Task<GameState?> GetGameStateAsync(string blockId);

    /// <summary>
    /// 获取指定 Block 当前 WorldState 中的单个实体。
    /// </summary>
    /// <param name="blockId">目标 Block 的 ID。</param>
    /// <param name="entityId">要查找的实体的 TypedID。</param>
    /// <param name="includeDestroyed">是否包含已标记为销毁的实体。</param>
    /// <returns>找到的实体，如果 Block 或实体不存在则返回 null。</returns>
    Task<BaseEntity?> GetEntityAsync(string blockId, TypedID entityId, bool includeDestroyed = false);

    /// <summary>
    /// 获取指定 Block 当前 WorldState 中的所有实体。
    /// </summary>
    /// <param name="blockId">目标 Block 的 ID。</param>
    /// <param name="includeDestroyed">是否包含已标记为销毁的实体。</param>
    /// <returns>实体列表，如果 Block 不存在则返回 null 或空列表。</returns>
    Task<IEnumerable<BaseEntity>?> GetAllEntitiesAsync(string blockId, bool includeDestroyed = false);

    /// <summary>
    /// 获取指定 Block 的原始文本内容 (raw_text)。
    /// </summary>
    /// <param name="blockId">目标 Block 的 ID。</param>
    /// <returns>Block 的 raw_text，如果 Block 不存在则返回 null。</returns>
    Task<string?> GetBlockRawTextAsync(string blockId);

    /// <summary>
    /// 获取从根到指定 Block 的父 Block ID 列表（按从根到父的顺序）。
    /// </summary>
    /// <param name="blockId">目标 Block 的 ID。</param>
    /// <returns>父 Block ID 列表。</returns>
    Task<List<string>> GetParentBlockIdsAsync(string blockId);

    // --- 根据需要添加其他查询方法 ---
    // 例如:
    // Task<WorldState?> GetWorldStateSnapshotAsync(string blockId, WorldStateSnapshotType type);
    // Task<object?> GetPlayerMetadataAsync(string key);
}