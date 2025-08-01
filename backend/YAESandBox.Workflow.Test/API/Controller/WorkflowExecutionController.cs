using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Utility;
using Microsoft.AspNetCore.Http;
using YAESandBox.Authentication;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService;

namespace YAESandBox.Workflow.Test.API.Controller;

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
    /// 执行一个工作流并返回最终结果。
    /// </summary>
    /// <remarks>
    /// 这是一个简化的执行端点，用于快速测试。
    /// 它接收一个完整的工作流配置和触发参数，然后同步执行整个流程。
    /// 在实际应用中，可能会使用 SignalR 进行流式返回。
    /// </remarks>
    /// <param name="request">包含工作流配置和触发参数的请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工作流的执行结果。</returns>
    [HttpPost("execute")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WorkflowExecutionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowExecutionResult>> ExecuteWorkflow(
        [FromBody] WorkflowExecutionRequest request,
        CancellationToken cancellationToken)
    {
        // 最终的文本结果
        string finalResultText = string.Empty;

        // 创建一个临时的回调，用于捕获最终结果
        var callback = new WorkflowRawTextCallbackTemp(
            _ => Task.CompletedTask, // 忽略流式更新
            finalRawText =>
            {
                finalResultText = finalRawText;
                return Task.CompletedTask;
            }
        );

        // 使用 Mock 的数据访问层
        var mockDataAccess = new MockWorkflowDataAccess();

        var processor = request.WorkflowConfig.ToWorkflowProcessor(
            request.TriggerParams,
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

        // 如果工作流成功，但我们想把最终文本也返回，可以包装一下
        // 这里我们简单地在成功结果的 ErrorMessage 字段里返回文本（这是一个临时的做法）
        return this.Ok(new WorkflowExecutionResult(true, finalResultText, null));
    }

    /// <summary>
    /// 以流式方式执行工作流，并通过 Server-Sent Events 返回结果。
    /// </summary>
    /// <remarks>
    /// 此端点用于对话式场景，实时返回AI生成的文本。
    /// 它会持续推送更新后的完整文本。
    /// </remarks>
    /// <param name="request">包含工作流配置和触发参数的请求体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpPost("execute-stream")]
    [Produces("text/event-stream")] // 明确返回类型为 SSE
    public async Task ExecuteWorkflowStream(
        [FromBody] WorkflowExecutionRequest request,
        CancellationToken cancellationToken)
    {
        // 1. 设置响应头为 text/event-stream
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        // 2. 创建一个 Channel 作为生产者-消费者队列
        var channel = Channel.CreateUnbounded<StreamMessage>();
        var streamingManager = new StreamingTextManager();

        // 3. 定义回调，它会将数据写入 Channel (生产者)
        var callback = new WorkflowRawTextCallbackTemp(
            requestDisplayUpdateCallback =>
            {
                var processedText = streamingManager.ProcessChunk(requestDisplayUpdateCallback.Content);
                var message = new StreamMessage("data", processedText);
                return channel.Writer.WriteAsync(message, cancellationToken).AsTask();
            },
            finalRawText =>
            {
                // 最终结果也写入，然后标记 Channel 完成
                streamingManager.ProcessChunk(finalRawText);
                return Task.CompletedTask;
            }
        );

        // 4. 在后台任务中启动工作流执行
        _ = Task.Run(async () =>
        {
            try
            {
                var mockDataAccess = new MockWorkflowDataAccess();
                var processor = request.WorkflowConfig.ToWorkflowProcessor(
                    request.TriggerParams,
                    this.MasterAiService.ToSubAiService(this.UserId),
                    mockDataAccess,
                    callback
                );
                var result = await processor.ExecuteWorkflowAsync(cancellationToken);

                // 如果工作流本身失败，发送一个错误事件
                if (!result.IsSuccess)
                {
                    var errorMessage = new StreamMessage("error", result.ErrorMessage);
                    await channel.Writer.WriteAsync(errorMessage, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // 捕获意外异常
                var exceptionMessage = new StreamMessage("error", $"[Backend Error] {ex.Message}");
                await channel.Writer.WriteAsync(exceptionMessage, cancellationToken);
            }
            finally
            {
                await channel.Writer.WriteAsync(new StreamMessage("done", null), cancellationToken);
                channel.Writer.Complete();
            }
        }, cancellationToken);


        // 5. 在主线程中消费 Channel 的数据并写入响应流
        try
        {
            // 当 Channel 中有数据时，循环读取 (消费者)
            await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
            {
                // 7. 直接序列化消息对象，不再进行二次包装
                var payload = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    // 在这里应用 UnsafeRelaxedJsonEscaping，确保中文正确
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    // 为了与前端的 StreamEventPayload (camelCase) 匹配
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
                await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                // 如果收到 'done' 消息，就提前退出循环
                if (message.Type == "done")
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 客户端断开连接，这是正常现象，无需处理
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
    public required WorkflowProcessorConfig WorkflowConfig { get; init; }

    /// <summary>
    /// 工作流启动所需的触发参数。
    /// Key 是参数名，Value 是参数值。
    /// </summary>
    public required Dictionary<string, string> TriggerParams { get; init; }
}

/// <summary>
/// 辅助管理流式文本的拼接、think标签提取和重组。
/// </summary>
public class StreamingTextManager
{
    private readonly StringBuilder _fullTextBuilder = new();
    private readonly StringBuilder _thinkingProcessBuilder = new();
    private static readonly Regex ThinkTagRegex = new("<think>(.*?)</think>", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// 处理一个新的文本片段。
    /// </summary>
    /// <param name="chunk">工作流回调提供的文本片段。</param>
    /// <returns>经过重组后的、准备发送给前端的完整文本。</returns>
    public string ProcessChunk(string chunk)
    {
        // 从新片段中提取<think>内容
        var match = ThinkTagRegex.Match(chunk);
        if (match.Success)
        {
            // 提取<think>标签内的内容并追加到思维过程缓冲区
            _thinkingProcessBuilder.Append(match.Groups[1].Value.Trim());

            // 从原片段中移除<think>标签，得到纯净内容
            chunk = ThinkTagRegex.Replace(chunk, string.Empty);
        }

        // 将纯净内容追加到主文本缓冲区
        _fullTextBuilder.Append(chunk);

        // 如果存在思维过程，则构建带<think>前缀的输出
        if (_thinkingProcessBuilder.Length > 0)
        {
            return $"<think>{_thinkingProcessBuilder}</think>\n{_fullTextBuilder}";
        }

        // 否则只返回当前拼接的文本
        return _fullTextBuilder.ToString();
    }

    /// <summary>
    /// 获取最终的、不含任何think标签的纯净文本。
    /// </summary>
    public string GetFinalText()
    {
        return _fullTextBuilder.ToString();
    }
}