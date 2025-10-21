using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Runtime.Processor;
using YAESandBox.Workflow.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.TestDoubles;
using static YAESandBox.Workflow.Runtime.Processor.TuumProcessor;

namespace YAESandBox.Workflow.API.Controller;

/// <summary>
/// 提供一个通用的、用于模拟运行任何符文的API。
/// </summary>
[ApiController]
[Route("api/v1/workflow/rune/mock-run")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class MockRuneController(IMasterAiService masterAiService) : AuthenticatedApiControllerBase
{
    private IMasterAiService MasterAiService { get; } = masterAiService;

    // --- DTO 定义 ---

    /// <summary>
    /// 测试请求的数据传输对象。
    /// </summary>
    public record MockRunRequestDto
    {
        /// <summary>
        /// 要测试的符文的完整配置。
        /// </summary>
        [Required]
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public AbstractRuneConfig RuneConfig { get; init; } = null!;

        /// <summary>
        /// 模拟的输入变量。
        /// Key是变量名，Value是用户提供的模拟值。
        /// </summary>
        [Required]
        public Dictionary<string, object?> MockInputs { get; init; } = new();
    }

    /// <summary>
    /// 测试响应的数据传输对象。
    /// </summary>
    public record MockRunResponseDto
    {
        /// <summary>
        /// 是否运行成功。
        /// </summary>
        [Required]
        public bool IsSuccess { get; init; }

        /// <summary>
        /// 失败时，这里是详细的错误信息。
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// 符文执行后产生的所有输出变量。
        /// Key是变量名，Value是执行后的结果。
        /// </summary>
        public Dictionary<string, object?> ProducedOutputs { get; init; } = new();

        /// <summary>
        /// 包含符文执行期间的详细调试信息。
        /// </summary>
        public IRuneProcessorDebugDto? DebugInfo { get; init; }
    }

    // --- API 方法 ---

    /// <summary>
    /// 执行一次即时测试。
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    public async Task<ActionResult<MockRunResponseDto>> RunTest([FromBody] MockRunRequestDto request)
    {
        string userId = this.UserId;
        // 1. 利用多态，让Config自己创建对应的Processor。
        var workflowRuntimeService =
            new FakeWorkflowRuntimeService(aiService: new SubAiService(this.MasterAiService, userId), userId: userId);
        var creatingContext = ProcessorContext.CreateRoot(Guid.NewGuid(), workflowRuntimeService);
        var processor = request.RuneConfig.ToRuneProcessor(creatingContext);
        var context = ProcessorContext.CreateRoot(Guid.NewGuid(), workflowRuntimeService);

        // 2. 创建一个模拟的、临时的枢机上下文
        var mockTuumContent = new TuumProcessorContent(
            new TuumConfig { ConfigId = "mock-run-tuum" },
            processorContext: context.ExtractContext()
        );

        // 3. 将所有模拟输入注入到上下文中
        foreach (var input in request.MockInputs)
        {
            mockTuumContent.SetTuumVar(input.Key, input.Value);
        }

        // 4. 执行 Processor
        var executionResult =
            await processor.ExecuteAsync(mockTuumContent);
        if (executionResult.TryGetError(out var error))
        {
            return this.Ok(new MockRunResponseDto
            {
                IsSuccess = false,
                ErrorMessage = error.ToDetailString(),
                DebugInfo = processor.DebugDto
            });
        }

        // 5. 动态地从上下文中取出所有预期的输出结果
        var producedOutputs = new Dictionary<string, object?>();
        // 通过配置的静态分析，我们知道应该期望哪些输出变量
        foreach (var spec in request.RuneConfig.GetProducedSpec())
        {
            producedOutputs[spec.Name] = mockTuumContent.GetTuumVar(spec.Name);
        }

        return this.Ok(new MockRunResponseDto
        {
            IsSuccess = true,
            ProducedOutputs = producedOutputs,
            DebugInfo = processor.DebugDto
        });
    }
}