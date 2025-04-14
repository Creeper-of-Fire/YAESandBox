using System.Threading.Tasks;
using YAESandBox.Core.State; // For BlockStatus
using YAESandBox.API.DTOs; // For DTOs like ConflictDetectedDto etc. if needed here

namespace YAESandBox.API.Services;

/// <summary>
/// 服务接口，用于将后端状态变更通知给前端（通常通过 SignalR）。
/// </summary>
public interface INotifierService
{
    /// <summary>
    /// 通知指定 Block 的状态已更新。
    /// </summary>
    /// <param name="blockId">Block ID。</param>
    /// <param name="newStatus">新的状态。</param>
    Task NotifyBlockStatusUpdateAsync(string blockId, BlockStatus newStatus);

    /// <summary>
    /// 通知指定 Block 的 WorldState 发生了变化（原子操作成功应用）。
    /// </summary>
    /// <param name="blockId">Block ID。</param>
    /// <param name="changedEntityIds">（可选）发生变更的实体 ID 列表。</param>
    Task NotifyStateUpdateAsync(string blockId, IEnumerable<string>? changedEntityIds = null);

    // --- 未来可能由 WorkflowService 或 BlockManager 在特定场景调用 ---
    // Task NotifyWorkflowUpdateAsync(WorkflowUpdateDto update);
    // Task NotifyWorkflowCompleteAsync(WorkflowCompleteDto completion);
    // Task NotifyConflictDetectedAsync(ConflictDetectedDto conflict);
}