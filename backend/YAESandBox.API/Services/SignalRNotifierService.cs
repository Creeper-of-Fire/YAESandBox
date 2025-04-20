using Microsoft.AspNetCore.SignalR;
using YAESandBox.API.DTOs;
using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.API.Hubs;
using YAESandBox.API.Services.InterFaceAndBasic;
using YAESandBox.Core.Block;
using YAESandBox.Depend;

namespace YAESandBox.API.Services;

/// <summary>
/// SignalRNotifierService 是一个实现 INotifierService 接口的服务，用于通过 SignalR 发送通知。
/// 在RestAPI中，不应该存在INotifierService的调用
/// </summary>
public class SignalRNotifierService(IHubContext<GameHub, IGameClient> hubContext, IBlockManager blockManager)
    : INotifierService, IWorkflowNotifierService
{
    private IHubContext<GameHub, IGameClient> hubContext { get; } = hubContext;
    private IBlockManager blockManager { get; } = blockManager;

    /// <inheritdoc/>
    public async Task NotifyBlockStatusUpdateAsync(string blockId, BlockStatusCode newStatusCode)
    {
        // Try to get the block to include parent info (this might be slightly racy if called during creation/deletion)
        // A better approach might be to pass parentId explicitly when calling this method if available.
        // For now, let's assume BlockManager handles passing necessary info or we fetch it here (less ideal).

        // Fetching block just to get parent ID adds overhead. Let's modify the interface/caller.
        // Option 1: Modify INotifierService interface (preferred)
        // Option 2: Pass parentId as optional arg here (less clean)

        // Assuming Option 1 or caller passes parentId, modify IBlockManager calls to pass it.
        // Let's simulate passing it for now. Need BlockManager changes.

        // *** TEMPORARY SIMULATION - Needs proper implementation in BlockManager ***
        // string? parentId = await GetParentIdFromBlockManagerAsync(blockId); // Imaginary method

        var update = new BlockStatusUpdateDto
        {
            BlockId = blockId,
            StatusCode = newStatusCode
            // ParentBlockId = parentId // <<< Set ParentBlockId here
            // For now, we can't easily get parentId here without BlockManager ref or interface change.
            // The frontend fix below will rely on sessionStorage fallback if ParentBlockId is null.
        };
        Log.Debug($"准备通过 SignalR 发送 BlockStatusUpdate: BlockId={blockId}, StatusCode={newStatusCode}");
        await this.hubContext.Clients.All.ReceiveBlockStatusUpdate(update);
        Log.Debug($"BlockStatusUpdate for {blockId} 已发送。");
    }

    /// <inheritdoc/>>
    public async Task NotifyBlockUpdateAsync(string blockId,
        IEnumerable<BlockDataFields> changedFields,
        IEnumerable<string>? changedEntityIds = null)
    {
        var signal = new StateUpdateSignalDto(blockId, ChangedEntityIds: changedEntityIds?.ToList());
        Log.Debug($"准备通过 SignalR 发送 StateUpdateSignal: BlockId={blockId}");
        await this.hubContext.Clients.All.ReceiveBlockUpdateSignal(signal);
        Log.Debug($"StateUpdateSignal for {blockId} 已发送。");
    }

    /// <inheritdoc/>>
    [Obsolete("目前我们不使用这个玩意，而是只通知可能发生变更的Block。", true)]
    public async Task NotifyBlockDetailUpdateAsync(string blockId, params BlockDetailFields[] changedFields)
    {
        var block = await this.blockManager.GetBlockAsync(blockId);
        if (block == null)
        {
            Log.Error($"BlockManagementService: 尝试发送 BlockDetailUpdateSignal 时，找不到 Block '{blockId}'。");
            return;
        }

        Log.Debug($"准备通过 SignalR 发送 BlockDetailUpdate: BlockId={blockId}");
        await this.hubContext.Clients.All.ReceiveBlockDetailUpdateSignal(block.CreatePartial(changedFields));
        Log.Debug($"StateUpdateSignal for {blockId} 已发送。");
    }

    /// <summary>
    /// 发送显示更新到前端
    /// </summary>
    /// <param name="update"></param>
    /// <returns></returns>
    public async Task NotifyDisplayUpdateAsync(DisplayUpdateDto update)
    {
        Log.Debug(
            $"准备通过 SignalR 发送 WorkflowUpdate: RequestId={update.RequestId}, BlockId={update.ContextBlockId}, UpdateMode={update.UpdateMode}");
        await this.hubContext.Clients.All.ReceiveDisplayUpdate(update);
        Log.Debug($"WorkflowUpdate for RequestId={update.RequestId} 已发送。");
    }

    /// <summary>
    /// 检测到指令冲突就发送这个通知
    /// </summary>
    /// <param name="conflict"></param>
    /// <returns></returns>
    public async Task NotifyConflictDetectedAsync(ConflictDetectedDto conflict)
    {
        Log.Debug($"准备通过 SignalR 发送 ConflictDetected: RequestId={conflict.RequestId}, BlockId={conflict.BlockId}");
        await this.hubContext.Clients.All.ReceiveConflictDetected(conflict);
        Log.Debug($"ConflictDetected for RequestId={conflict.RequestId} 已发送。");
    }
}