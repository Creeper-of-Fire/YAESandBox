using Microsoft.AspNetCore.Mvc;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Utility;
using Microsoft.AspNetCore.Http;
using YAESandBox.Workflow.AIService;

namespace YAESandBox.Workflow.Test.API.Controller;

/// <summary>
/// 提供工作流执行和测试的功能。
/// </summary>
[ApiController]
[Route("api/v1/workflow-execution")]
[ApiExplorerSettings(GroupName = WorkflowTestModule.WorkflowTestGroupName)]
public class WorkflowExecutionController(IMasterAiService masterAiService) : ControllerBase
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
            this.MasterAiService,
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
}

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