using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Workflow.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using YAESandBox.Authentication;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.Core.Abstractions;
using YAESandBox.Workflow.Test.API.GameHub;

namespace YAESandBox.Workflow.Test.API.Controller;

/// <summary>
/// 一个临时的回调实现，用于将工作流的发射事件路由到不同的处理程序。
/// </summary>
file class EmitterCallback(
    Func<EmitPayload, Task<Result>> onEmitAsync,
    Func<Task>? onCompleteAsync = null
) : IWorkflowCallback, IWorkflowEventEmitter
{
    public Task<Result> EmitAsync(EmitPayload payload, CancellationToken cancellationToken = default)
    {
        // 简单地调用外部提供的委托
        return onEmitAsync(payload);
    }

    public async Task CompleteAsync()
    {
        if (onCompleteAsync != null)
        {
            await onCompleteAsync();
        }
    }
}

/// <summary>
/// 提供工作流执行和测试的功能。
/// </summary>
[ApiController]
[Route("api/v1/workflow-execution")]
[ApiExplorerSettings(GroupName = WorkflowTestModule.WorkflowTestGroupName)]
public class WorkflowExecutionController(
    IMasterAiService masterAiService,
    IHubContext<WorkflowHub> workflowHubContext
) : AuthenticatedApiControllerBase
{
    private IMasterAiService MasterAiService { get; } = masterAiService;
    private IHubContext<WorkflowHub> WorkflowHubContext { get; } = workflowHubContext;

    /// <summary>
    /// 执行一个工作流并返回最终结果（以结构化文本形式）。
    /// </summary>
    /// <remarks>
    /// 这是一个简化的同步执行端点。它会捕获所有发射的事件，
    /// 在内存中构建一个结构化的XML响应，并最终返回完整的XML字符串。
    /// </remarks>
    [HttpPost("execute")]
    [Produces("application/json")] // 依然返回JSON，但内容是包含XML的WorkflowExecutionResult
    [ProducesResponseType(typeof(WorkflowExecutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowExecutionResult>> ExecuteWorkflow(
        [FromBody] WorkflowExecutionRequest request,
        CancellationToken cancellationToken)
    {
        // 1. 使用 StructuredContentBuilder 来捕获所有事件
        var contentBuilder = new StructuredContentBuilder("response");

        // 2. 创建新的 EmitterCallback
        var callback = new EmitterCallback(
            onEmitAsync: payload =>
            {
                // 将所有事件都路由到 StructuredContentBuilder
                // 我们假设所有发射的数据都是字符串或可以转换为字符串
                try
                {
                    contentBuilder.SetContent(payload.Address, payload.Data?.ToString() ?? "", payload.Mode);
                }
                catch (Exception e)
                {
                    return Task.FromResult(Result.Fail(e.Message).ToResult());
                }

                return Task.FromResult(Result.Ok());
            }
        );

        // 使用 Mock 的数据访问层
        var mockDataAccess = new MockWorkflowDataAccess();

        var processor = request.WorkflowConfig.ToWorkflowProcessor(
            request.WorkflowInputs,
            this.MasterAiService.ToSubAiService(this.UserId),
            mockDataAccess,
            callback
        );

        var result = await processor.ExecuteWorkflowAsync(cancellationToken);

        // 如果工作流本身执行失败，直接返回错误
        if (!result.IsSuccess)
        {
            return this.Ok(result);
        }

        // 3. 将最终构建的结构化文本作为结果返回
        // 我们在成功结果的 ErrorMessage 字段里返回文本（这是一个临时的做法，符合旧逻辑）
        return this.Ok(new WorkflowExecutionResult(true, contentBuilder.ToString(), null));
    }


    /// <summary>
    /// 通过 SignalR 异步触发一个工作流执行，并流式推送结果。
    /// </summary>
    /// <remarks>
    /// 此端点会立即返回202 Accepted状态，表示任务已接受。
    /// 实际的工作流在后台执行，并通过与请求中 `ConnectionId` 关联的 SignalR 连接推送事件。
    /// 客户端需要先建立SignalR连接，获取`ConnectionId`，然后调用此API。
    /// </remarks>
    [HttpPost("execute-signalr")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult ExecuteWorkflowSignalR(
        [FromBody] WorkflowExecutionSignalRRequest request,
        CancellationToken cancellationToken)
    {
        // 1. StructuredContentBuilder 用于在内存中聚合状态
        var contentBuilder = new StructuredContentBuilder("response");

        // 2. 定义新的 EmitterCallback，它将通过 SignalR Hub 发送消息
        var callback = new EmitterCallback(
            onEmitAsync: async payload =>
            {
                // a. 更新内存中的XML树
                contentBuilder.SetContent(payload.Address, payload.Data?.ToString() ?? "", payload.Mode);

                // b. 将更新后的完整XML字符串通过SignalR发送给特定的客户端
                var message = new StreamMessage("data", contentBuilder.ToString());
                await this.WorkflowHubContext.Clients.Client(request.ConnectionId)
                    .SendAsync("ReceiveWorkflowUpdate", message, cancellationToken);

                return Result.Ok();
            }
        );

        // 3. 在后台任务中启动工作流执行 (与SSE版本类似，但通信方式不同)
        _ = Task.Run<Task>(async () =>
        {
            try
            {
                var mockDataAccess = new MockWorkflowDataAccess();
                var processor = request.WorkflowConfig.ToWorkflowProcessor(
                    request.WorkflowInputs,
                    this.MasterAiService.ToSubAiService(this.UserId),
                    mockDataAccess,
                    callback
                );
                var result = await processor.ExecuteWorkflowAsync(cancellationToken);

                var finalMessage = result.IsSuccess
                    ? new StreamMessage("done", "Workflow completed successfully.")
                    : new StreamMessage("error", result.ErrorMessage);

                await this.WorkflowHubContext.Clients.Client(request.ConnectionId)
                    .SendAsync("ReceiveWorkflowUpdate", finalMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                // 捕获意外异常
                var exceptionMessage = new StreamMessage("error", $"[Backend Error] {ex.Message}");
                // 确保即使在取消的情况下也尝试通知客户端
                await this.WorkflowHubContext.Clients.Client(request.ConnectionId)
                    .SendAsync("ReceiveWorkflowUpdate", exceptionMessage, CancellationToken.None);
            }
        }, cancellationToken);

        // 4. 立即返回，表示请求已被接受并在后台处理
        return this.Accepted();
    }
}

// 定义 Channel 中传递的消息结构
internal record StreamMessage(string Type, string? Content);

/// <summary>
/// 工作流执行请求的 DTO。
/// </summary>
public record WorkflowExecutionRequest
{
    /// <summary>
    /// 要执行的工作流的完整配置。
    /// </summary>
    [Required]
    public required WorkflowConfig WorkflowConfig { get; init; }

    /// <summary>
    /// 工作流启动所需的触发参数。
    /// Key 是参数名，Value 是参数值。
    /// </summary>
    [Required]
    public required Dictionary<string, string> WorkflowInputs { get; init; }
}

/// <summary>
/// 用于 SignalR 触发的工作流执行请求的 DTO。
/// </summary>
public record WorkflowExecutionSignalRRequest : WorkflowExecutionRequest
{
    /// <summary>
    /// 客户端的 SignalR 连接 ID。
    /// 服务器将通过此 ID 将流式结果推送给正确的客户端。
    /// </summary>
    [Required]
    public required string ConnectionId { get; init; }
}