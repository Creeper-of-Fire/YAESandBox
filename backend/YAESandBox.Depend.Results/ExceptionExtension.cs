using System.Text;

namespace YAESandBox.Depend.Results;

/// <summary>
/// 提供用于格式化 Exception 对象的扩展方法。
/// </summary>
public static class ExceptionExtension
{
    /// <summary>
    /// 将异常及其所有内部异常转换为详细的、格式化的字符串。
    /// </summary>
    /// <param name="ex">要格式化的异常对象。</param>
    /// <param name="includeStackTrace">是否在输出中包含堆栈跟踪信息。</param>
    /// <returns>一个包含异常类型、消息、内部异常和堆栈跟踪的字符串。</returns>
    public static string ToFormattedString(this Exception? ex, bool includeStackTrace = true)
    {
        if (ex is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var currentException = ex;
        int level = 0;

        sb.AppendLine("--- 错误详情 ---");

        while (currentException != null)
        {
            if (level > 0)
            {
                sb.AppendLine();
                sb.Append(' ', level * 2); // 缩进以表示内部异常
                sb.AppendLine("--- 内部错误 ---");
            }

            sb.Append(' ', level * 2);
            sb.AppendLine($"错误类型: {currentException.GetType().FullName}");

            sb.Append(' ', level * 2);
            sb.AppendLine($"错误信息: {currentException.Message}");

            if (includeStackTrace && !string.IsNullOrWhiteSpace(currentException.StackTrace))
            {
                sb.Append(' ', level * 2);
                sb.AppendLine("堆栈内容：");
                
                // 对堆栈跟踪的每一行进行缩进，使其更易读
                using var reader = new StringReader(currentException.StackTrace);
                while (reader.ReadLine() is { } line)
                {
                    sb.Append(' ', level * 2 + 2); // 额外缩进
                    sb.AppendLine(line.Trim());
                }
            }

            currentException = currentException.InnerException;
            level++;
        }
        
        sb.AppendLine("-------------------------");

        return sb.ToString();
    }
}