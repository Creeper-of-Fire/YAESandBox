// using Microsoft.AspNetCore.SignalR;
// using YAESandBox.Depend.Results;
// using YAESandBox.Workflow.Core.Abstractions;
//
// namespace YAESandBox.Workflow.Test.API.GameHub;
//
// /// <summary>
// /// 用于工作流实时执行的 SignalR Hub。
// /// </summary>
// public class WorkflowHub : Hub
// {
//     /// <summary>
//     /// 这个方法是可选的，但为我们未来的“异步状态访问器”模式做好了准备。
//     /// 服务器端的 Rune (通过一个服务) 可以调用这个方法来向前端请求数据。
//     /// </summary>
//     /// <param name="queryType"></param>
//     /// <param name="queryPayload"></param>
//     /// <returns></returns>
//     public async Task<object?> QueryGameState(string queryType, object? queryPayload)
//     {
//         // 调用客户端名为 "QueryGameState" 的方法，并传递查询参数
//         // InvokeAsync 会等待客户端的返回值
//         return await this.Clients.Caller.InvokeAsync<object?>("QueryGameState", queryType, queryPayload, this.Context.ConnectionAborted);
//     }
// }
//
// /// <summary>
// /// 允许工作流异步查询前端游戏状态的接口。
// /// </summary>
// public interface IAsyncGameStateAccessor
// {
//     /// <summary>
//     /// 查询前端的状态。
//     /// </summary>
//     /// <typeparam name="T">期望返回的类型。</typeparam>
//     /// <param name="queryType">查询的类型标识符。</param>
//     /// <param name="queryPayload">查询所需的参数。</param>
//     /// <returns>从前端返回的状态数据。</returns>
//     Task<T?> QueryAsync<T>(string queryType, object? queryPayload = null);
// }
//
// /// <summary>
// /// 使用 SignalR 实现的游戏状态访问器。
// /// </summary>
// file class SignalRGameStateAccessor(IHubContext<WorkflowHub> hubContext, string connectionId) : IAsyncGameStateAccessor
// {
//     public Task<T?> QueryAsync<T>(string queryType, object? queryPayload = null)
//     {
//         // 调用特定客户端的 "QueryGameState" 方法并等待其返回结果。
//         return hubContext.Clients.Client(connectionId).InvokeAsync<T?>("QueryGameState", queryType, queryPayload);
//     }
// }
//
// /// <summary>
// /// 一个回调实现，用于将工作流的发射事件通过 SignalR 推送到客户端。
// /// </summary>
// file class SignalREmitterCallback(IHubContext<WorkflowHub> hubContext, string connectionId) : IWorkflowCallback, IWorkflowEventEmitter
// {
//     public async Task<Result> EmitAsync(EmitPayload payload, CancellationToken cancellationToken = default)
//     {
//         var message = new StreamMessage(payload.Address, payload.Data?.ToString(), payload.Mode.ToString());
//         await hubContext.Clients.Client(connectionId).SendAsync("WorkflowEvent", message, cancellationToken);
//         return Result.Ok();
//     }
//
//     public Task CompleteAsync() => Task.CompletedTask;
// }