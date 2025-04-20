// --- START OF FILE GameHub.cs ---

using Microsoft.AspNetCore.SignalR;
using YAESandBox.API.DTOs;
using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.API.Services;
using YAESandBox.API.Services.InterFaceAndBasic; // Need WorkflowService
using YAESandBox.Depend; // For Log

namespace YAESandBox.API.Hubs;

public interface IGameClient
{
    /// <summary>
    /// Block的状态码更新了
    /// 收到这个讯号以后建议做一次全量更新
    /// </summary>
    /// <param name="update"></param>
    /// <returns></returns>
    Task ReceiveBlockStatusUpdate(BlockStatusUpdateDto update);

    /// <summary>
    /// 更新特定Block/控件的显示内容
    /// </summary>
    /// <param name="update"></param>
    /// <returns></returns>
    Task ReceiveDisplayUpdate(DisplayUpdateDto update);

    /// <summary>
    /// 检测到主工作流存在冲突
    /// </summary>
    /// <param name="conflict"></param>
    /// <returns></returns>
    Task ReceiveConflictDetected(ConflictDetectedDto conflict);

    /// <summary>
    /// Block内部的WorldState/GameState存在更新，建议重新获取
    /// </summary>
    /// <param name="signal"></param>
    /// <returns></returns>
    Task ReceiveBlockUpdateSignal(StateUpdateSignalDto signal);

    /// <summary>
    /// Block的非WorldState/GameState内容存在更新，建议重新获取
    /// </summary>
    /// <param name="signal"></param>
    /// <returns></returns>
    Task ReceiveBlockUpdateSignal(string signal);

    /// <summary>
    /// Block的详细信息更新了，比如显示内容、父子结构或者Metadata（不包含DisplayUpdate发起的那些内容更新）
    /// </summary>
    /// <param name="partiallyFilledDto">部分填充的 BlockDetailDto，包含各种已更新的字段。</param>
    /// <returns></returns>
    [Obsolete("目前我们不使用这个玩意，而是只通知可能发生变更的Block。", true)]
    Task ReceiveBlockDetailUpdateSignal(BlockDetailDto partiallyFilledDto);
}

public class GameHub(IWorkflowService workflowService) : Hub<IGameClient>
{
    private IWorkflowService workflowService { get; } = workflowService;

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
            await this.workflowService.HandleMainWorkflowTriggerAsync(request);
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
            await this.workflowService.HandleMicroWorkflowTriggerAsync(request);
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
            await this.workflowService.HandleConflictResolutionAsync(request);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"GameHub: ResolveConflict 处理时发生异常: {ex.Message}");
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