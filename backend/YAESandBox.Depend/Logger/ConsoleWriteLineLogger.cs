using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;

namespace YAESandBox.Depend.Logger;

/// <inheritdoc />
internal class ConsoleWriteLineLoggerFactory : IAppLoggerFactory
{
    /// <inheritdoc />
    public IAppLogger CreateLogger(string categoryName) => new ConsoleWriteLineLogger(categoryName);

    /// <inheritdoc />
    public IAppLogger<T> CreateLogger<T>() => new ConsoleWriteLineLogger<T>();
}

/// <inheritdoc cref="IAppLogger{T}"/>
file class ConsoleWriteLineLogger<T>() : ConsoleWriteLineLogger(typeof(T).Name), IAppLogger<T>;

/// <inheritdoc />
internal partial class ConsoleWriteLineLogger(string categoryName) : IAppLogger
{
    private string CategoryName { get; } = categoryName;

    [GeneratedRegex(@"\{([\p{L}\p{N}_]+)(:[^\}]+)?\}", RegexOptions.Compiled)]
    private static partial Regex GetStructuredTemplateRegex();

    private static Regex StructuredTemplateRegex { get; } = GetStructuredTemplateRegex();

    // 辅助方法，用于安全地格式化字符串
    private static string FormatMessage(string message, params object?[] args)
    {
        // 如果没有参数，直接返回消息，避免不必要的格式化
        if (args.Length == 0)
        {
            return message;
        }

        try
        {
            // 1. 将模板和参数转换成一个字典
            var properties = new Dictionary<string, object?>();
            var matches = StructuredTemplateRegex.Matches(message);
            for (int i = 0; i < matches.Count; i++)
            {
                // 如果参数不足，则停止匹配，防止越界
                if (i >= args.Length) break;

                string key = matches[i].Groups[1].Value;
                properties[key] = args[i];
            }

            // 2. 将模板中的 {Placeholder} 替换为实际值
            string flatMessage = StructuredTemplateRegex.Replace(message, match =>
            {
                string key = match.Groups[1].Value;
                // 如果字典中有值，则替换；否则保留原样
                return properties.TryGetValue(key, out object? value) ? (value?.ToString() ?? "null") : match.Value;
            });

            // 3. 将所有属性作为类似 JSON 的字符串附加在后面，模拟结构化输出
            string structuredPart = string.Join(", ", properties.Select(kv => $"\"{kv.Key}\": \"{kv.Value}\""));

            return $"{flatMessage} [{structuredPart}]";
        }
        catch (FormatException ex)
        {
            // 如果格式化失败，返回原始消息并附带错误，确保日志系统不会崩溃
            return $"[LOG_FORMAT_ERROR: {message}] {ex.ToFormattedString()}";
        }
    }

    /// <inheritdoc />
    public void Trace(string message, params object?[] args) =>
        Console.WriteLine($"[Trace:{this.CategoryName}]{FormatMessage(message, args)}");

    /// <inheritdoc />
    public void Debug(string message, params object?[] args) =>
        Console.WriteLine($"[Debug:{this.CategoryName}]{FormatMessage(message, args)}");

    /// <inheritdoc />
    public void Info(string message, params object?[] args) =>
        Console.WriteLine($"[Info:{this.CategoryName}]{FormatMessage(message, args)}");

    /// <inheritdoc />
    public void Warn(string message, params object?[] args) =>
        Console.WriteLine($"[Warn:{this.CategoryName}]{FormatMessage(message, args)}");

    /// <inheritdoc />
    public void Error(string message, params object?[] args) =>
        Console.WriteLine($"[Error:{this.CategoryName}]{FormatMessage(message, args)}");

    /// <inheritdoc />
    public void Error(Exception exception, string message, params object?[] args) =>
        Console.WriteLine($"[Error:{this.CategoryName}]{exception}\n{FormatMessage(message, args)}");

    /// <inheritdoc />
    public void Critical(string message, params object?[] args) =>
        Console.WriteLine($"[Critical:{this.CategoryName}]{FormatMessage(message, args)}");

    /// <inheritdoc />
    public void Critical(Exception exception, string message, params object?[] args) =>
        Console.WriteLine($"[Critical:{this.CategoryName}]{exception}\n{FormatMessage(message, args)}");
}