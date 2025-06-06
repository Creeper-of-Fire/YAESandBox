// --- START OF FILE GameHub.cs ---

using Microsoft.AspNetCore.SignalR;
using YAESandBox.Core.DTOs.WebSocket;
using YAESandBox.Core.Services.InterFaceAndBasic;
using YAESandBox.Depend;

// Need WorkflowService

// For Log

namespace YAESandBox.Core.API.Hubs;

public class GameHub(IWorkflowService workflowService) : Hub<IGameClient>
{
    private IWorkflowService WorkflowService { get; } = workflowService;

    /// <summary>
    /// 触发主工作流
    /// </summary>
    /// <param name="request"></param>
    public async Task TriggerMainWorkflow(TriggerMainWorkflowRequestDto request)
    {
        string connectionId = this.Context.ConnectionId;
        // *** 确认日志存在 ***
        Log.Info(
            $"GameHub: 收到来自 {connectionId} 的主工作流触发请求: {request.RequestId}, Workflow: {request.WorkflowName}, ParentBlock: {request.ParentBlockId}");
        try
        {
            await this.WorkflowService.HandleMainWorkflowTriggerAsync(request);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"GameHub: TriggerMainWorkflow 处理时发生异常: {ex.Message}");
            // 可选: await Clients.Caller.SendAsync("ReceiveError", "Failed to initiate workflow.");
        }
    }

    /// <summary>
    /// 触发微工作流
    /// </summary>
    /// <param name="request"></param>
    public async Task TriggerMicroWorkflow(TriggerMicroWorkflowRequestDto request)
    {
        string connectionId = this.Context.ConnectionId;
        Log.Info(
            $"GameHub: 收到来自 {connectionId} 的微工作流触发请求: {request.RequestId}, Workflow:{request.WorkflowName}, Element:{request.TargetElementId}, Block:{request.ContextBlockId}");
        try
        {
            await this.WorkflowService.HandleMicroWorkflowTriggerAsync(request);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"GameHub: TriggerMicroWorkflow 处理时发生异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 解决冲突
    /// </summary>
    /// <param name="request"></param>
    public async Task ResolveConflict(ResolveConflictRequestDto request)
    {
        string connectionId = this.Context.ConnectionId;
        // *** 确认日志存在 ***
        Log.Info($"GameHub: 收到来自 {connectionId} 的 ResolveConflict 请求: {request.RequestId}, Block: {request.BlockId}");
        try
        {
            await this.WorkflowService.HandleConflictResolutionAsync(request);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"GameHub: ResolveConflict 处理时发生异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 重新生成
    /// </summary>
    /// <param name="request"></param>
    public async Task RegenerateBlock(RegenerateBlockRequestDto request)
    {
        string connectionId = this.Context.ConnectionId;
        // *** 确认日志存在 ***
        Log.Info($"GameHub: 收到来自 {connectionId} 的 RegenerateBlock 请求: {request.RequestId}, Block: {request.BlockId}");
        try
        {
            await this.WorkflowService.HandleRegenerateBlockAsync(request);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"GameHub: RegenerateBlock 处理时发生异常: {ex.Message}");
        }
    }

    // --- Hub 生命周期事件 ---
    ///<inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        // *** 确认日志存在 ***
        Log.Info($"GameHub: Client connected. ConnectionId: {this.Context.ConnectionId}");
        // 尝试获取更多信息 (可能为 null)
        var httpContext = this.Context.GetHttpContext();
        var remoteIp = httpContext?.Connection.RemoteIpAddress;
        var userAgent = httpContext?.Request.Headers.UserAgent;
        Log.Debug($"GameHub: Connection details - Remote IP: {remoteIp}, User-Agent: {userAgent}");
        await base.OnConnectedAsync();
    }

    ///<inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // *** 确认日志存在 ***
        Log.Info(
            $"GameHub: Client disconnected. ConnectionId: {this.Context.ConnectionId}. Reason: {exception?.Message ?? "Normal disconnect"}");
        if (exception != null) Log.Error(exception, $"GameHub: Disconnect exception details for {this.Context.ConnectionId}:");

        await base.OnDisconnectedAsync(exception);
    }
}
// --- END OF FILE GameHub.cs ---