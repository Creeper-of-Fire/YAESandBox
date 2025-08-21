using Microsoft.AspNetCore.SignalR;

namespace YAESandBox.Workflow.Test.API.GameHub;

/// <summary>
/// 用于工作流执行的实时通信 Hub。
/// 客户端连接此 Hub 以接收来自工作流执行过程的流式更新。
/// </summary>
public class WorkflowHub : Hub
{
    // 目前我们不需要客户端调用服务器的方法，
    // Hub 的主要作用是建立一个持久连接，让服务器可以主动推送消息。
    // 你可以在这里添加未来可能需要的方法，例如 Client-to-Server 的 "CancelExecution"。
}