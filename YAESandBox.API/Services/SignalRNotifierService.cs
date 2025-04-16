using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using YAESandBox.API.DTOs;
using YAESandBox.API.Hubs;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Depend;

namespace YAESandBox.API.Services;

public class SignalRNotifierService : INotifierService
{
    private IHubContext<GameHub, IGameClient> hubContext { get; }

    public SignalRNotifierService(IHubContext<GameHub, IGameClient> hubContext)
    {
        this.hubContext = hubContext;
         Log.Debug("SignalRNotifierService 初始化完成。");
    }

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

    public async Task NotifyStateUpdateAsync(string blockId, IEnumerable<string>? changedEntityIds = null)
    {
        var signal = new StateUpdateSignalDto
        {
            BlockId = blockId
            // ChangedEntityIds = changedEntityIds?.ToList() ?? new List<string>() // 可选
        };
         Log.Debug($"准备通过 SignalR 发送 StateUpdateSignal: BlockId={blockId}");
        await this.hubContext.Clients.All.ReceiveStateUpdateSignal(signal);
         Log.Debug($"StateUpdateSignal for {blockId} 已发送。");
    }

     // --- 实现其他通知方法 ---
     public async Task NotifyWorkflowUpdateAsync(WorkflowUpdateDto update)
     {
         Log.Debug($"准备通过 SignalR 发送 WorkflowUpdate: RequestId={update.RequestId}, BlockId={update.BlockId}, Type={update.UpdateType}");
         // 广播给所有客户端，前端根据 BlockId 决定是否显示
         await this.hubContext.Clients.All.ReceiveWorkflowUpdate(update);
         Log.Debug($"WorkflowUpdate for RequestId={update.RequestId} 已发送。");
     }

     public async Task NotifyWorkflowCompleteAsync(WorkflowCompleteDto completion)
     {
         Log.Debug($"准备通过 SignalR 发送 WorkflowComplete: RequestId={completion.RequestId}, BlockId={completion.BlockId}, StatusCode={completion.ExecutionStatus}");
         await this.hubContext.Clients.All.ReceiveWorkflowComplete(completion);
         Log.Debug($"WorkflowComplete for RequestId={completion.RequestId} 已发送。");
     }

     public async Task NotifyConflictDetectedAsync(ConflictDetectedDto conflict)
     {
          Log.Debug($"准备通过 SignalR 发送 ConflictDetected: RequestId={conflict.RequestId}, BlockId={conflict.BlockId}");
         await this.hubContext.Clients.All.ReceiveConflictDetected(conflict);
          Log.Debug($"ConflictDetected for RequestId={conflict.RequestId} 已发送。");
     }
}