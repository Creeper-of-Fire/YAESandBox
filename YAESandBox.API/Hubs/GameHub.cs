using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using YAESandBox.API.DTOs; // 使用 DTOs
using YAESandBox.API.Services; // 假设有 WorkflowService

namespace YAESandBox.API.Hubs;

/// <summary>
/// 定义客户端可以调用的方法。
/// </summary>
public interface IGameClient
{
    Task ReceiveBlockStatusUpdate(BlockStatusUpdateDto update);
    Task ReceiveWorkflowUpdate(WorkflowUpdateDto update);
    Task ReceiveWorkflowComplete(WorkflowCompleteDto completion);
    Task ReceiveConflictDetected(ConflictDetectedDto conflict);
    Task ReceiveStateUpdateSignal(StateUpdateSignalDto signal);
    // 可以添加其他客户端需要响应的方法，如强制刷新、显示全局消息等
}

/// <summary>
/// 处理游戏相关的实时通信。
/// </summary>
public class GameHub : Hub<IGameClient> // 强类型 Hub
{
    private readonly WorkflowService _workflowService; // 注入服务
    // 可能还需要注入 BlockManager 或其他服务

    public GameHub(WorkflowService workflowService /* ... other services */)
    {
        _workflowService = workflowService;
    }

    /// <summary>
    /// 客户端调用此方法来触发一个（通常是一等公民）工作流。
    /// </summary>
    /// <param name="request">包含触发所需信息的请求对象。</param>
    public async Task TriggerWorkflow(TriggerWorkflowRequestDto request)
    {
        // 获取调用者的连接 ID (如果需要特定于用户的操作)
        // var connectionId = Context.ConnectionId;

        // 调用 WorkflowService 处理请求
        // WorkflowService 内部会创建 Block，调用 AI，更新状态，
        // 并通过注入的 NotifierService (最终调用 HubContext) 发送更新给客户端
        await _workflowService.HandleWorkflowTriggerAsync(request);

        // 注意：这里通常不需要直接调用 Clients.Caller.SendAsync(...)
        // 状态更新应该由后端服务（如 NotifierService）通过 HubContext 推送给所有相关客户端
    }

    /// <summary>
    /// 客户端调用此方法来提交冲突解决方案。
    /// </summary>
    /// <param name="request">包含解决冲突所需信息的请求对象。</param>
    public async Task ResolveConflict(ResolveConflictRequestDto request)
    {
        // 调用 WorkflowService 或 BlockManager 处理冲突解决
        await _workflowService.HandleConflictResolutionAsync(request);
        // 同样，状态更新由后端服务推送
    }

    // --- Hub 生命周期事件 (可选) ---
    public override async Task OnConnectedAsync()
    {
        // 用户连接时可以执行的操作，例如加入某个默认组
        // await Groups.AddToGroupAsync(Context.ConnectionId, "defaultGroup");
        // logger.LogInformation($"Client connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // 用户断开连接时执行清理操作
        // logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }
}