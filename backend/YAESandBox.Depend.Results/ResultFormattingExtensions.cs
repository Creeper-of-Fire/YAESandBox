using System.Reflection;
using System.Text;

namespace YAESandBox.Depend.Results;

/// <summary>
/// 提供用于将 Result 对象转换为详细字符串表示的扩展方法。
/// </summary>
public static class ResultFormattingExtensions
{
    /// <summary>
    /// 将 Result 对象转换为详细、人类可读的字符串，适用于日志和调试。
    /// </summary>
    /// <param name="result">要转换的 Result 对象。</param>
    /// <returns>一个描述 Result 状态的详细字符串。</returns>
    public static string ToDetailString(this Result result)
    {
        if (!result.TryGetError(out var error))
            return "操作成功完成。";
        return error.ToDetailString();
    }

    /// <summary>
    /// 将 Result&lt;T&gt; 对象转换为详细、人类可读的字符串。
    /// </summary>
    public static string ToDetailString<T>(this Result<T> result)
    {
        if (result.TryGetValue(out var value))
        {
            return $"操作成功完成。值为: {value?.ToString() ?? "null"}";
        }

        // 如果失败，则调用非泛型版本的 ToDetailString() 来处理错误
        return result.ToResult().ToDetailString();
    }

    /// <summary>
    /// 将 Error 对象及其子类的所有信息转换为详细、人类可读的字符串。
    /// 使用反射来自动发现并打印出 Error 子类的所有公共属性。
    /// <remarks>
    /// 在开发环境中，此方法会返回完整的异常堆栈信息。在生产环境中，为了安全，可以配置为返回通用错误消息。
    /// </remarks>
    /// </summary>
    /// <param name="error">要格式化的 Error 对象。</param>
    /// <param name="includeStackTrace">是否在输出中包含堆栈跟踪信息。</param>
    /// <returns>一个描述 Error 内容的详细字符串。</returns>
    public static string ToDetailString(this Error error, bool includeStackTrace = true)
    {
        var errorType = error.GetType();
        // 1. 检查是否为最基础的 Error 类型
        bool isBaseError = errorType == typeof(Error);
        
        // 2. 动态获取自定义属性
        var customProperties = errorType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.DeclaringType != typeof(Error) && p.CanRead)
            .Select(p => new { p.Name, Value = p.GetValue(error) })
            .Where(p => p.Value is not null && (p.Value is not string s || !string.IsNullOrEmpty(s)))
            .ToList(); // 物化结果以避免多次枚举
        
        // 3. 判断是否为“简单错误”：基础 Error 类型 + 无自定义属性 + 无异常
        bool isSimpleError = isBaseError && customProperties.Count == 0 && error.Exception is null;
        
        if (isSimpleError)
        {
            // 对于最简单的错误，只返回消息本身，保持极度简洁。
            return error.Message;
        }
        
        var sb = new StringBuilder();
        sb.AppendLine("--- 操作失败详情 ---");

        // 如果不是基础 Error 类型，才显示具体的错误类型
        if (!isBaseError)
        {
            sb.AppendLine($"错误类型: {errorType.Name}");
        }
        
        sb.AppendLine($"错误消息: {error.Message}");

        // 打印所有找到的自定义属性
        foreach (var prop in customProperties)
        {
            // 在这之前我们已经过滤了 null 值
            sb.AppendLine($"{prop.Name}: {prop.Value}");
        }

        // 如果存在内部异常，则附加完整的异常信息
        if (error.Exception is not null)
        {
            sb.AppendLine("--- 内部异常详情 ---");
            // 复用我们之前创建的 ToFormattedString() 扩展方法
            sb.Append(error.Exception.ToFormattedString(includeStackTrace));
        }

        sb.AppendLine("--------------------");

        return sb.ToString();
    }
}