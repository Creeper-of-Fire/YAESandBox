using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 项目通用的JsonHelper
/// </summary>
public static class YAESandBoxJsonHelper
{
    /// <summary>
    /// 项目通用的JsonSerializerOptions
    /// </summary>
    public static JsonSerializerOptions JsonSerializerOptions { get; } =
        new()
        {
            WriteIndented = true, // 格式化输出
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // 忽略 null 值属性
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 允许不安全的字符编码 (如中文不转码)
            Converters = { new JsonStringEnumConverter() },
            PropertyNameCaseInsensitive = true
        };
}