namespace YAESandBox.Depend.Secret;

/// <summary>
/// 一个密钥保护器
/// </summary>
public interface ISecretProtector
{
    /// <summary>
    /// 加密一个明文字符串。
    /// </summary>
    /// <param name="plaintext">明文字符串</param>
    /// <returns>加密后字符串</returns>
    string Protect(string plaintext);

    /// <summary>
    /// 解密一个受保护字符串。
    /// </summary>
    /// <param name="protectedData">受保护的字符串</param>
    /// <returns>解密后的明文字符串</returns>
    string Unprotect(string protectedData);
}