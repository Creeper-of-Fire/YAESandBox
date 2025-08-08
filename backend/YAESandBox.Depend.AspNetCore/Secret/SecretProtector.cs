using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace YAESandBox.Depend.AspNetCore.Secret;

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

/// <summary>
/// 默认的密钥保护器实现
/// </summary>
/// <param name="provider"></param>
public class SecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    // 定义一个常量来存储我们的加密前缀，避免硬编码字符串
    private const string ProtectionPrefix = "enc::v1::";

    // 这是一个“用途字符串”，确保这些加密数据只能被同样用途的Protector解密
    private IDataProtector Protector { get; } = provider.CreateProtector("YAESandBox.Secrets");

    /// <inheritdoc />
    public string Protect(string plaintext)
    {
        // 如果输入为空或已经是加密格式（避免重复加密），则直接返回
        if (!IsProtected(plaintext))
        {
            return plaintext;
        }

        string protectedPayload = this.Protector.Protect(plaintext);

        return $"{ProtectionPrefix}{protectedPayload}";
    }

    /// <inheritdoc />
    public string Unprotect(string protectedData)
    {
        // 如果输入为空或不是加密格式，则直接返回
        if (!IsProtected(protectedData))
        {
            return protectedData;
        }
        
        string protectedPayload = protectedData[ProtectionPrefix.Length..];

        try
        {
            return this.Protector.Unprotect(protectedPayload);
        }
        catch (CryptographicException)
        {
            // 如果解密失败（例如，密钥环已更改或数据已损坏），返回一个提示信息
            return "[[DECRYPTION_FAILED]]";
        }
    }

    private static bool IsProtected(string data)
    {
        return !string.IsNullOrEmpty(data) && data.StartsWith(ProtectionPrefix, StringComparison.OrdinalIgnoreCase);
    }
}