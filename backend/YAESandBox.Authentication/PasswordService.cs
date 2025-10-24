namespace YAESandBox.Authentication;

/// <summary>
/// 密码服务接口，定义了密码哈希和验证的功能。
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// 对纯文本密码进行哈希处理。
    /// </summary>
    /// <param name="password">纯文本密码。</param>
    /// <returns>哈希后的密码字符串（包含盐和成本参数）。</returns>
    string HashPassword(string password);
    
    /// <summary>
    /// 验证纯文本密码是否与存储的哈希值匹配。
    /// </summary>
    /// <param name="password">用户输入的纯文本密码。</param>
    /// <param name="passwordHash">数据库中存储的哈希密码。</param>
    /// <returns>如果匹配返回 true，否则返回 false。</returns>
    bool VerifyPassword(string password, string passwordHash);
}

/// <summary>
/// 使用 BCrypt.Net 实现的密码服务。
/// BCrypt 是一种强大的密码哈希算法，内置了自动加盐和成本管理。
/// </summary>
public class PasswordService : IPasswordService
{
    /// <inheritdoc />
    /// <remarks>BCrypt 会自动处理加盐（salt）和迭代次数（work factor），非常方便。</remarks>
    public string HashPassword(string password) => 
        BCrypt.Net.BCrypt.HashPassword(password);

    /// <inheritdoc />
    public bool VerifyPassword(string password, string passwordHash) => 
        BCrypt.Net.BCrypt.Verify(password, passwordHash);
}