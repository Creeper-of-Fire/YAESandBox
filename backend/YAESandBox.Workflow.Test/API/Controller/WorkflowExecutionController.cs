using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using YAESandBox.Authentication.API;
using YAESandBox.Authentication.Storage;
using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Core.Config;
using YAESandBox.Workflow.Core.Runtime.InstanceId;
using YAESandBox.Workflow.Core.Runtime.Processor;
using YAESandBox.Workflow.Core.Runtime.RuntimePersistence;
using YAESandBox.Workflow.Core.Runtime.RuntimePersistence.Storage;
using YAESandBox.Workflow.Core.Runtime.WorkflowService;
using YAESandBox.Workflow.Core.Runtime.WorkflowService.Abstractions;
using YAESandBox.Workflow.Test.API.GameHub;
using YAESandBox.Workflow.Utility;

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
/// 定义流式输出的格式。
/// </summary>
public enum StreamOutputFormat
{
    /// <summary>
    /// 输出自定义的类 XML 格式。
    /// </summary>
    Xml,

    /// <summary>
    /// 输出标准 JSON 格式。
    /// </summary>
    Json
}

/// <summary>
/// 提供工作流执行和测试的功能。
/// </summary>
[ApiController]
[Route("api/v1/workflow-execution")]
[ApiExplorerSettings(GroupName = WorkflowTestModule.WorkflowTestGroupName)]
public class WorkflowExecutionController(
    IMasterAiService masterAiService,
    IHubContext<WorkflowHub> workflowHubContext,
    IUserScopedStorageFactory userScopedStorageFactory,
    WorkflowConfigFindService workflowConfigFindService
) : AuthenticatedApiControllerBase
{
    private IMasterAiService MasterAiService { get; } = masterAiService;
    private IHubContext<WorkflowHub> WorkflowHubContext { get; } = workflowHubContext;
    private IUserScopedStorageFactory UserScopedStorageFactory { get; } = userScopedStorageFactory;
    private WorkflowConfigFindService WorkflowConfigFindService { get; } = workflowConfigFindService;
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger<WorkflowExecutionController>();

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
    public async Task<IActionResult> ExecuteWorkflowSignalR(
        [FromBody] WorkflowExecutionSignalRRequest request,
        CancellationToken cancellationToken)
    {
        // 1. StructuredContentBuilder 用于在内存中聚合状态
        var contentBuilder = new StructuredContentBuilder("response");

        var workflowInstanceId = InstanceIdGenerator.CreateForNewWorkflow();

        string userId = this.UserId;

        // 2. 定义新的 EmitterCallback，它将通过 SignalR Hub 发送消息
        var callback = new EmitterCallback(
            onEmitAsync: async payload =>
            {
                // a. 更新内存中的树
                contentBuilder.SetContent(payload.Address, payload.Data?.ToString() ?? "", payload.Mode, mergeTags: false);

                // b. 根据请求的格式选择序列化方法
                string serializedContent = request.OutputFormat switch
                {
                    StreamOutputFormat.Json => contentBuilder.ToJson(),
                    _ => contentBuilder.ToString() // 默认为 XML
                };

                // c. 将更新后的完整XML字符串通过SignalR发送给特定的客户端
                var message = new StreamMessage("data", serializedContent);
                await this.WorkflowHubContext.Clients.Client(request.ConnectionId)
                    .SendAsync("ReceiveWorkflowUpdate", message, cancellationToken);

                return Result.Ok();
            }
        );

        var scopeTemplate = JsonFilePersistenceStorage.PersistenceScope;
        var scopedJsonStorageResult = await this.UserScopedStorageFactory.GetFinalStorageForUserAsync(userId, scopeTemplate);
        IPersistenceStorage persistenceStorageStorage;
        if (scopedJsonStorageResult.TryGetError(out var scopedJsonStorageError, out var scopedJsonStorage))
        {
            Logger.Error(scopedJsonStorageError);
            persistenceStorageStorage = new InMemoryPersistenceStorage();
        }
        else
        {
            persistenceStorageStorage = new JsonFilePersistenceStorage(scopedJsonStorage,workflowInstanceId);
        }

        // 3. 在后台任务中启动工作流执行 (与SSE版本类似，但通信方式不同)
        _ = Task.Run<Task>(async () =>
        {
            try
            {
                var mockDataAccess = new MockWorkflowDataAccess();
                var processor = request.WorkflowConfig.ToWorkflowProcessor(
                    workflowInstanceId,
                    request.WorkflowInputs,
                    this.MasterAiService.ToSubAiService(userId),
                    mockDataAccess,
                    callback,
                    new WorkflowPersistenceService(persistenceStorageStorage),
                    this.WorkflowConfigFindService,
                    userId
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
                var exceptionMessage = new StreamMessage("error", $"工作流执行期间发生未处理的异常：{ex.ToFormattedString()}");
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

    /// <summary>
    /// 指定期望的流式输出格式。
    /// 默认为 Xml。(实际均为字符串)
    /// </summary>
    [Required]
    public StreamOutputFormat OutputFormat { get; init; } = StreamOutputFormat.Xml;
}