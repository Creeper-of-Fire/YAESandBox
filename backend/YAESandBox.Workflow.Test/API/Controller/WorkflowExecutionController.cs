using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Workflow.Utility;
using Microsoft.AspNetCore.Http;
using YAESandBox.Authentication;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.Core.Abstractions;

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
public class WorkflowExecutionController(IMasterAiService masterAiService) : AuthenticatedApiControllerBase
{
    private IMasterAiService MasterAiService { get; } = masterAiService;

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
    /// 以流式方式执行工作流，并通过 Server-Sent Events 返回结构化结果。
    /// </summary>
    /// <remarks>
    /// 此端点使用我们新的事件系统。工作流中的 "EmitEventRune" 会触发事件，
    /// 此处会将这些事件实时地构建成一个XML结构，并将每次更新后的完整XML通过SSE推送给前端。
    /// </remarks>
    [HttpPost("execute-stream")]
    [Produces("text/event-stream")]
    public async Task ExecuteWorkflowStream(
        [FromBody] WorkflowExecutionRequest request,
        CancellationToken cancellationToken)
    {
        // 1. 设置响应头
        this.Response.Headers.Append("Content-Type", "text/event-stream");
        this.Response.Headers.Append("Cache-Control", "no-cache");
        this.Response.Headers.Append("Connection", "keep-alive");

        // 2. 创建 Channel 和 StructuredContentBuilder
        var channel = Channel.CreateUnbounded<StreamMessage>();
        var contentBuilder = new StructuredContentBuilder("response");

        // 3. 定义新的 EmitterCallback
        var callback = new EmitterCallback(
            onEmitAsync: async payload =>
            {
                // a. 使用 StructuredContentBuilder 更新内存中的XML树
                contentBuilder.SetContent(payload.Address, payload.Data?.ToString() ?? "", payload.Mode);

                // b. 将更新后的完整XML字符串写入 Channel
                var message = new StreamMessage("data", contentBuilder.ToString());
                await channel.Writer.WriteAsync(message, cancellationToken);

                return Result.Ok();
            },
            onCompleteAsync: () => Task.FromResult(Result.Ok())
        );

        // 4. 在后台任务中启动工作流执行
        _ = Task.Run(async () =>
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

                // 根据工作流执行结果发送最终消息
                if (result.IsSuccess)
                {
                    // a. 如果成功，发送 'done' 消息
                    var doneMessage = new StreamMessage("done", "Workflow completed successfully.");
                    await channel.Writer.WriteAsync(doneMessage, cancellationToken);
                }
                else
                {
                    // b. 如果是业务逻辑上的失败（例如，输入验证失败），发送 'error' 消息
                    var errorMessage = new StreamMessage("error", result.ErrorMessage);
                    await channel.Writer.WriteAsync(errorMessage, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                var exceptionMessage = new StreamMessage("error", $"[Backend Error] {ex.Message}");
                await channel.Writer.WriteAsync(exceptionMessage, cancellationToken);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);


        // 5. 在主线程中消费 Channel 的数据并写入响应流
        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
            {
                string payload = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await this.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                await this.Response.Body.FlushAsync(cancellationToken);

                if (message.Type is "done" or "error")
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 客户端断开连接
        }
    }
}

// 定义 Channel 中传递的消息结构
file record StreamMessage(string Type, string? Content);

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