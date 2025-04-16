// --- START OF FILE GameHub.cs ---
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services; // Need WorkflowService
using YAESandBox.Depend; // For Log

namespace YAESandBox.API.Hubs;

public interface IGameClient
{
    Task ReceiveBlockStatusUpdate(BlockStatusUpdateDto update);
    Task ReceiveWorkflowUpdate(WorkflowUpdateDto update); // For progress/logs
    Task ReceiveWorkflowComplete(WorkflowCompleteDto completion); // Final result/error
    Task ReceiveConflictDetected(ConflictDetectedDto conflict);
    Task ReceiveStateUpdateSignal(StateUpdateSignalDto signal);
    // Task ReceiveWorkflowError(WorkflowErrorDto error); // Consider specific error message type
}
public class GameHub(IWorkflowService workflowService) : Hub<IGameClient>
{
    private IWorkflowService workflowService { get; } = workflowService;

    // --- TriggerWorkflow 和 ResolveConflict 不变 ---
     public async Task TriggerWorkflow(TriggerWorkflowRequestDto request)
    {
        var connectionId = this.Context.ConnectionId;
        // *** 确认日志存在 ***
        Log.Info($"GameHub: 收到来自 {connectionId} 的 TriggerWorkflow 请求: {request.RequestId}, Workflow: {request.WorkflowName}");
        try
        {
            await this.workflowService.HandleWorkflowTriggerAsync(request);
        }
        catch(Exception ex)
        {
             Log.Error(ex, $"GameHub: TriggerWorkflow 处理时发生异常: {ex.Message}");
             // 可选: await Clients.Caller.SendAsync("ReceiveError", "Failed to initiate workflow.");
        }
    }
    public async Task ResolveConflict(ResolveConflictRequestDto request)
    {
         var connectionId = this.Context.ConnectionId;
         // *** 确认日志存在 ***
         Log.Info($"GameHub: 收到来自 {connectionId} 的 ResolveConflict 请求: {request.RequestId}, Block: {request.BlockId}");
         try
         {
             await this.workflowService.HandleConflictResolutionAsync(request);
         }
         catch (Exception ex)
         {
              Log.Error(ex, $"GameHub: ResolveConflict 处理时发生异常: {ex.Message}");
         }
    }

    // --- Hub 生命周期事件 ---
    public override async Task OnConnectedAsync()
    {
        // *** 确认日志存在 ***
        Log.Info($"GameHub: Client connected. ConnectionId: {this.Context.ConnectionId}");
        // 尝试获取更多信息 (可能为 null)
        var httpContext = this.Context.GetHttpContext();
        var remoteIp = httpContext?.Connection.RemoteIpAddress;
        var userAgent = httpContext?.Request.Headers["User-Agent"];
        Log.Debug($"GameHub: Connection details - Remote IP: {remoteIp}, User-Agent: {userAgent}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
         // *** 确认日志存在 ***
        Log.Info($"GameHub: Client disconnected. ConnectionId: {this.Context.ConnectionId}. Reason: {exception?.Message ?? "Normal disconnect"}");
        if (exception != null)
        {
            Log.Error(exception, $"GameHub: Disconnect exception details for {this.Context.ConnectionId}:");
        }
        await base.OnDisconnectedAsync(exception);
    }
}
// --- END OF FILE GameHub.cs ---