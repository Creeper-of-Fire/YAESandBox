using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.AIService;

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
    /// <returns>包含最终响应</returns>
    Task<Result<string>> NonStreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        NonStreamRequestCallBack? requestCallBack = null,
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
    public required Action<string> OnChunkReceived { get; init; }
}

/// <inheritdoc />
/// <remarks>用于非流式</remarks>
public record NonStreamRequestCallBack : BaseRequestCallBack
{
    // 目前这里为空，只继承了 TokenUsage
    // 未来如果非流式有其他特定回调，可以加在这里
}