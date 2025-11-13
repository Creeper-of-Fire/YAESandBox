namespace YAESandBox.Depend.Results;

/// <summary>
/// 一个可安全序列化为 JSON 的错误记录，用于持久化。
/// 它从一个 Exception 对象中提取核心信息，并舍弃无法序列化的运行时属性（如 TargetSite）。
/// </summary>
public record SerializableError
{
    /// <summary>
    /// 异常的类型名称。
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>
    /// 错误消息。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 堆栈跟踪信息。
    /// </summary>
    public string? StackTrace { get; init; }
    
    /// <summary>
    /// 内部错误的序列化表示。
    /// </summary>
    public SerializableError? InnerError { get; init; }

    /// <summary>
    /// 从一个 Exception 对象创建一个可序列化的错误记录。
    /// </summary>
    /// <param name="ex">要转换的异常。</param>
    /// <returns>一个包含核心错误信息的新 <see cref="SerializableError"/> 实例。</returns>
    public static SerializableError FromException(Exception ex)
    {
        return new SerializableError
        {
            ExceptionType = ex.GetType().FullName,
            Message = ex.Message,
            StackTrace = ex.StackTrace,
            InnerError = ex.InnerException is not null ? FromException(ex.InnerException) : null
        };
    }
    
    /// <summary>
    /// 从一个错误对象创建一个可序列化的错误记录。
    /// </summary>
    /// <param name="error">要转换的错误记录。</param>
    /// <returns>一个包含错误信息的新 <see cref="SerializableError"/> 实例。</returns>
    public static SerializableError FromError(Error error)
    {
        var serializableError = error.Exception is not null
            ? SerializableError.FromException(error.Exception)
            : new SerializableError { Message = error.Message };
        return serializableError;
    }
    
    /// <summary>
    /// 将持久化的错误信息转换回一个 Error 对象，以便在运行时使用。
    /// 注意：转换后的 Error 对象将不包含原始的 Exception 实例，但会保留其核心信息。
    /// </summary>
    public Error ToError()
    {
        var reconstructedException = new PersistedException(this);
        return new Error(this.Message, reconstructedException);
    }
}

/// <summary>
/// 一个自定义异常类，用于包装从持久化存储中恢复的错误信息。
/// </summary>
public class PersistedException(SerializableError error)
    : Exception(error.Message, error.InnerError is not null ? new PersistedException(error.InnerError) : null)
{
    /// <summary>
    /// 原始异常的类型名称。
    /// </summary>
    public string? OriginalExceptionType { get; } = error.ExceptionType;

    /// <summary>
    /// 异常的堆栈跟踪信息。
    /// </summary>
    public override string StackTrace { get; } = error.StackTrace ?? string.Empty;

    /// <inheritdoc />
    public override string ToString()
    {
        // 提供更丰富的错误信息
        return $"被持久化的错误信息 (原始类型: {this.OriginalExceptionType}): {this.Message}\n堆栈信息:\n{this.StackTrace}";
    }
}