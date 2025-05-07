using FluentResults;

namespace YAESandBox.Workflow.AIService;

/// <summary>
/// 单个的AI处理流程，这是有状态的，因此不能被复用。
/// </summary>
public interface IAiProcessor
{
    /// <summary>
    /// 向 AI 服务发起流式请求 (修正版)
    /// </summary>
    /// <param name="prompts">完整的提示词</param>
    /// <param name="onChunkReceived">
    /// 当接收到新的数据块时调用的回调函数。
    /// !! 只传递新的数据块 (string chunk) !!
    /// </param>
    /// <param name="cancellationToken">用于取消操作</param>
    /// <returns>
    /// 不包含最终完整响应，因为内容的累积不是流式服务的主要职责，而且违背了唯一真相的原则。
    /// 整个函数只返回错误信息。
    /// </returns>
    Task<Result> StreamRequestAsync(
        List<(PromptRole role, string prompt)> prompts,
        Action<string> onChunkReceived,
        CancellationToken cancellationToken = default
    );

    // ... 可能还有非流式请求的方法，或者把流式/非流式打包为同一个方法？ ...
}

/// <summary>
/// 提示词中扮演的角色的枚举
/// </summary>
public record PromptRole
{
    /// <summary>
    /// 一个枚举，表示提示词的角色。
    /// </summary>
    public required PromptRoleType type { get; init; }

    /// <summary>
    /// 部分高级AI模型可以识别角色名称，因此可以指定角色名称。
    /// </summary>
    public string name { get; private init; } = "";
    
    public static PromptRole System(string name = "") => new() { type = PromptRoleType.System, name = name };
    public static PromptRole User(string name = "") => new() { type = PromptRoleType.User, name = name };
    public static PromptRole Assistant(string name = "") => new() { type = PromptRoleType.Assistant, name = name };
}

public enum PromptRoleType
{
    System,
    User,
    Assistant
}