using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService.Shared;

namespace YAESandBox.Workflow.AIService;

// TODO 之后可能要为了function call等情况进行重构，目前保持简单。

/// <summary>
/// 单个的AI处理流程，这是有状态的，因此不能被复用。
/// </summary>
public interface IAiProcessor
{
    /// <summary>
    /// 向 AI 服务发起流式请求
    /// </summary>
    /// <param name="prompts">完整的提示词</param>
    /// <param name="requestCallBack">可用的回调函数</param>
    /// <param name="cancellationToken">用于取消操作</param>
    /// <returns>
    /// 不包含最终完整响应，因为内容的累积不是流式服务的主要职责，而且违背了唯一真相的原则。
    /// 整个函数只返回错误信息。
    /// </returns>
    Task<Result> StreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        StreamRequestCallBack requestCallBack,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 向 AI 服务发起非流式请求
    /// </summary>
    /// <param name="prompts">完整的提示词</param>
    /// <param name="requestCallBack">可用的回调函数</param>
    /// <param name="cancellationToken">用于取消操作</param>
    /// <returns>只返回执行过程中的错误，成功时不包含任何数据。</returns>
    Task<Result> NonStreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        NonStreamRequestCallBack requestCallBack,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// AI Processor 可用的回调函数
/// </summary>
public abstract record BaseRequestCallBack
{
    /// <summary>
    /// 返回Token的使用统计。
    /// </summary>
    public Action<int> TokenUsage { get; init; } = _ => { };
}

/// <inheritdoc />
/// <remarks>用于流式</remarks>
public record StreamRequestCallBack : BaseRequestCallBack
{
    /// <summary>
    /// 当接收到新的数据块时调用的回调函数。
    /// !! 只传递新的数据块 (string chunk) !!
    /// </summary>
    public required Func<AiStructuredChunk,Task<Result>> OnChunkReceivedAsync { get; init; }
}

/// <inheritdoc />
/// <remarks>用于非流式</remarks>
public record NonStreamRequestCallBack : BaseRequestCallBack
{
    /// <summary>
    /// 当接收到完整的、最终的响应时调用的回调函数。
    /// </summary>
    public required Func<AiStructuredChunk,Task<Result>> OnFinalResponseReceivedAsync { get; init; }
}