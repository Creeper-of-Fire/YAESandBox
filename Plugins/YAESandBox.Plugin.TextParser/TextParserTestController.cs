using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Depend.Results;
using YAESandBox.Plugin.TextParser.Rune;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune.Config;
using YAESandBox.Workflow.Rune.Interface;
using YAESandBox.Workflow.Runtime;
using YAESandBox.Workflow.TestDoubles;
using YAESandBox.Workflow.Tuum;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Plugin.TextParser;

/// <summary>
/// 为文本处理器插件提供私有的、用于即时测试的API终结点。
/// 这个Controller不会出现在Swagger文档中。
/// </summary>
[ApiController]
[Route("api/v1/plugins/text-parser")]
[ApiExplorerSettings(IgnoreApi = true)] // 关键：不在Swagger中显示此API
public class TextParserTestController : ControllerBase
{
    // --- DTO 定义 ---

    /// <summary>
    /// 测试请求的数据传输对象。
    /// </summary>
    public record TestRequestDto
    {
        /// <summary>
        /// 要测试的符文的完整配置。
        /// 使用 AbstractRuneConfig 是因为前端会发送完整的JSON对象，
        /// 而我们的自定义JsonConverter会正确地将其反序列化为具体的Config类型。
        /// </summary>
        [Required]
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public AbstractRuneConfig RuneConfig { get; init; } = null!;

        /// <summary>
        /// 用户在前端测试文本框中输入的示例文本。
        /// </summary>
        [Required]
        public string SampleInputText { get; init; } = string.Empty;
    }

    /// <summary>
    /// 测试响应的数据传输对象。
    /// </summary>
    public record TestResponseDto
    {
        [Required] public bool IsSuccess { get; init; }

        /// <summary>
        /// 成功时，这里是提取或生成的结果。
        /// 使用 object? 是因为它可能是 string 或 List{string?}。
        /// </summary>
        public object? Result { get; init; }

        /// <summary>
        /// 失败时，这里是详细的错误信息。
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// 包含符文执行期间的详细调试信息。
        /// </summary>
        public IRuneProcessorDebugDto? DebugInfo { get; init; }
    }

    // --- API 方法 ---

    /// <summary>
    /// 执行一次即时测试。
    /// </summary>
    [HttpPost("run-test")]
    [Produces("application/json")]
    public async Task<ActionResult<TestResponseDto>> RunTest([FromBody] TestRequestDto request)
    {
        // 1. 根据传入的 Config 类型，在内存中动态创建对应的 Processor
        IProcessorWithDebugDto<IRuneProcessorDebugDto> processor;
        string inputVariableName;
        string outputVariableName;

        var workflowRuntimeService = new FakeWorkflowRuntimeService();
        var context = ProcessorContext.CreateRoot(Guid.NewGuid(), workflowRuntimeService);
        
        switch (request.RuneConfig)
        {
            case TagParserRuneConfig tagConfig:
                processor = new TagParserRuneProcessor(tagConfig,context);
                inputVariableName = tagConfig.TextOperation.InputVariableName;
                outputVariableName = tagConfig.TextOperation.OutputVariableName;
                break;
            case RegexParserRuneConfig regexConfig:
                processor = new RegexParserRuneProcessor(regexConfig,context);
                inputVariableName = regexConfig.TextOperation.InputVariableName;
                outputVariableName = regexConfig.TextOperation.OutputVariableName;
                break;
            default:
                return this.BadRequest(new TestResponseDto { IsSuccess = false, ErrorMessage = "不支持的符文配置类型。" });
        }

        // 2. 创建一个模拟的、临时的枢机上下文
        var mockTuumContent = new TuumProcessorContent(
            // 这两个参数在本次测试中不会被用到，可以传入null或默认值
            // 但为了健壮性，我们还是创建它们
            tuumConfig: new TuumConfig { ConfigId = "test-tuum" },
            processorContext: context.ExtractContext()
        );

        // 3. 将示例文本注入到模拟上下文中
        mockTuumContent.SetTuumVar(inputVariableName, request.SampleInputText);

        // 4. 执行 Processor
        var executionResult = await ((INormalRune<AbstractRuneConfig, IRuneProcessorDebugDto>)processor).ExecuteAsync(mockTuumContent);
        if (executionResult.TryGetError(out var error))
        {
            return this.Ok(new TestResponseDto
            {
                IsSuccess = false,
                ErrorMessage = error.ToDetailString(),
                DebugInfo = processor.DebugDto
            });
        }

        // 从模拟上下文中取出结果
        object? output = mockTuumContent.GetTuumVar(outputVariableName);
        return this.Ok(new TestResponseDto
        {
            IsSuccess = true,
            Result = output,
            DebugInfo = processor.DebugDto
        });
    }
}