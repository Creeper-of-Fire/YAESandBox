using System.ComponentModel.DataAnnotations;

namespace YAESandBox.Workflow.AIService;

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
    [Required]
    [DataType(DataType.Password)]
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
    /// <summary>
    /// 系统
    /// </summary>
    System,

    /// <summary>
    /// 用户
    /// </summary>
    User,

    /// <summary>
    /// 助手
    /// </summary>
    Assistant
}

/// <summary>
/// 
/// </summary>
public static class PromptRoleTypeExtension
{
    /// <summary>
    /// 把字符串转为角色类型
    /// </summary>
    /// <param name="roleString">
    /// 可用字符串：system, user, assistant
    /// </param>
    /// <returns></returns>
    public static PromptRoleType ToPromptRoleType(string roleString)
    {
        return roleString switch
        {
            "system" => PromptRoleType.System,
            "user" => PromptRoleType.User,
            "assistant" => PromptRoleType.Assistant,
            _ => PromptRoleType.User
        };
    }
}