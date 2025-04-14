using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using YAESandBox.API.DTOs; // 需要 DTOs
using YAESandBox.API.Hubs;  // 需要 GameHub 和 IGameClient
using YAESandBox.Core.State; // 需要 BlockStatus
using YAESandBox.Depend; // For Log

namespace YAESandBox.API.Services;

public class SignalRNotifierService : INotifierService
{
    private readonly IHubContext<GameHub, IGameClient> _hubContext;

    public SignalRNotifierService(IHubContext<GameHub, IGameClient> hubContext)
    {
        _hubContext = hubContext;
         Log.Debug("SignalRNotifierService 初始化完成。");
    }

    public async Task NotifyBlockStatusUpdateAsync(string blockId, BlockStatus newStatus)
    {
        var update = new BlockStatusUpdateDto
        {
            BlockId = blockId,
            Status = newStatus
        };
         Log.Debug($"准备通过 SignalR 发送 BlockStatusUpdate: BlockId={blockId}, Status={newStatus}");
        // 向所有连接的客户端广播状态更新
        // 未来可以考虑分组，只发送给关注此 Block 的客户端
        await _hubContext.Clients.All.ReceiveBlockStatusUpdate(update);
         Log.Debug($"BlockStatusUpdate for {blockId} 已发送。");
    }

    public async Task NotifyStateUpdateAsync(string blockId, IEnumerable<string>? changedEntityIds = null)
    {
        var signal = new StateUpdateSignalDto
        {
            BlockId = blockId
            // 可以选择性地填充 changedEntityIds，如果前端需要
            // ChangedEntityIds = changedEntityIds?.ToList() ?? new List<string>()
        };
         Log.Debug($"准备通过 SignalR 发送 StateUpdateSignal: BlockId={blockId}");
        // 向所有连接的客户端广播状态变更信号
        await _hubContext.Clients.All.ReceiveStateUpdateSignal(signal);
         Log.Debug($"StateUpdateSignal for {blockId} 已发送。");
    }

    // --- 其他通知方法的实现 (如果需要从 BlockManager 或其他地方触发) ---
    // public async Task NotifyWorkflowUpdateAsync(WorkflowUpdateDto update)
    // {
    //     Log.Debug($"准备通过 SignalR 发送 WorkflowUpdate: RequestId={update.RequestId}, BlockId={update.BlockId}");
    //     // 这个通常发送给特定的客户端（触发者）或某个组
    //     // await _hubContext.Clients.Client(connectionId).ReceiveWorkflowUpdate(update); // 或 Clients.Group(...)
    //     await _hubContext.Clients.All.ReceiveWorkflowUpdate(update); // 示例：广播给所有人
    //     Log.Debug($"WorkflowUpdate for RequestId={update.RequestId} 已发送。");
    // }

    // public async Task NotifyWorkflowCompleteAsync(WorkflowCompleteDto completion)
    // {
    //     Log.Debug($"准备通过 SignalR 发送 WorkflowComplete: RequestId={completion.RequestId}, BlockId={completion.BlockId}");
    //     await _hubContext.Clients.All.ReceiveWorkflowComplete(completion); // 示例：广播
    //     Log.Debug($"WorkflowComplete for RequestId={completion.RequestId} 已发送。");
    // }

    // public async Task NotifyConflictDetectedAsync(ConflictDetectedDto conflict)
    // {
    //      Log.Debug($"准备通过 SignalR 发送 ConflictDetected: RequestId={conflict.RequestId}, BlockId={conflict.BlockId}");
    //     await _hubContext.Clients.All.ReceiveConflictDetected(conflict); // 示例：广播
    //      Log.Debug($"ConflictDetected for RequestId={conflict.RequestId} 已发送。");
    // }
}