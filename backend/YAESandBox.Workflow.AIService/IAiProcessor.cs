using System.ComponentModel.DataAnnotations;
using FluentResults;

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
        List<RoledPromptDto> prompts,
        Action<string> onChunkReceived,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 向 AI 服务发起非流式请求
    /// </summary>
    /// <param name="prompts">完整的提示词</param>
    /// <param name="cancellationToken">用于取消操作</param>
    /// <returns>包含最终响应</returns>
    Task<Result<string>> NonStreamRequestAsync(
        List<RoledPromptDto> prompts,
        CancellationToken cancellationToken = default);

    // ... 可能还有非流式请求的方法，或者把流式/非流式打包为同一个方法？ ...
}

// /// <summary>
// /// 提示词中扮演的角色的枚举
// /// </summary>
// public record PromptRole
// {
//     /// <summary>
//     /// 一个枚举，表示提示词的角色。
//     /// </summary>
//     public required PromptRoleType type { get; init; }
//
//     /// <summary>
//     /// 部分高级AI模型可以识别角色名称，因此可以指定角色名称。
//     /// </summary>
//     public string name { get; private init; } = "";
//
//     public static PromptRole System(string name = "") => new() { type = PromptRoleType.System, name = name };
//     public static PromptRole User(string name = "") => new() { type = PromptRoleType.User, name = name };
//     public static PromptRole Assistant(string name = "") => new() { type = PromptRoleType.Assistant, name = name };
// }

/// <summary>
/// 提示词消息，包含角色和内容
/// </summary>
public record RoledPromptDto
{
    /// <summary>
    /// 一个枚举，表示提示词的角色。
    /// </summary>
    [Required]
    public required PromptRoleType Type { get; init; }

    /// <summary>
    /// 部分高级AI模型可以识别角色名称，因此可以指定角色名称。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 一段提示词
    /// </summary>
    [DataType(DataType.MultilineText)]
    [Required]
    public required string Content { get; init; } = "";

    /// <summary>
    /// 生成系统提示词
    /// </summary>
    /// <param name="prompt">提示词</param>
    /// <param name="name">部分高级AI模型可以识别角色名称，因此可以指定角色名称。</param>
    /// <returns></returns>
    public static RoledPromptDto System(string prompt, string name = "") =>
        new() { Type = PromptRoleType.System, Content = prompt, Name = name };

    /// <summary>
    /// 生成用户提示词
    /// </summary>
    /// <param name="prompt">提示词</param>
    /// <param name="name">部分高级AI模型可以识别角色名称，因此可以指定角色名称。</param>
    /// <returns></returns>
    public static RoledPromptDto User(string prompt, string name = "") =>
        new() { Type = PromptRoleType.User, Content = prompt, Name = name };

    /// <summary>
    /// 生成助手提示词（即预输入的AI回复）
    /// </summary>
    /// <param name="prompt">提示词</param>
    /// <param name="name">部分高级AI模型可以识别角色名称，因此可以指定角色名称。</param>
    /// <returns></returns>
    public static RoledPromptDto Assistant(string prompt, string name = "") =>
        new() { Type = PromptRoleType.Assistant, Content = prompt, Name = name };
}

/// <summary>
/// 提示词的角色类型
/// </summary>
public enum PromptRoleType
{
    System,
    User,
    Assistant
}