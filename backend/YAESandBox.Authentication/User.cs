using System.ComponentModel.DataAnnotations;

namespace YAESandBox.Authentication;

/// <summary>
/// 表示系统中的用户实体，包含用户的基本信息和认证数据
/// </summary>
public record User
{
    /// <summary>
    /// 用户的唯一标识符
    /// </summary>
    [Required]
    public required string Id { get; init; }

    /// <summary>
    /// 用户名，用于登录和显示
    /// </summary>
    [Required]
    public required string Username { get; init; }

    /// <summary>
    /// 用户密码的哈希值，用于安全验证
    /// </summary>
    [Required]
    public required string PasswordHash { get; init; }
}