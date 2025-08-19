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
    
    /// <summary>
    /// 刷新令牌，用于刷新访问令牌。可为空，因为用户可能从未登录过。
    /// </summary>
    public string? RefreshToken { get; set; }
    
    /// <summary>
    /// 刷新令牌过期时间，用于验证刷新令牌有效性。可为空，因为用户可能从未登录过。
    /// </summary>
    public DateTime? RefreshTokenExpiryTime { get; set; }
}