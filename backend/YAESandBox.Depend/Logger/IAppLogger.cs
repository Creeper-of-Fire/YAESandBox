using JetBrains.Annotations;

namespace YAESandBox.Depend.Logger;

/// <summary>
/// 一个自定义的日志工厂接口，用于创建日志记录器。
/// </summary>
internal interface IAppLoggerFactory
{
    /// <summary>
    /// 创建一个日志记录器，使用类名作为日志源。
    /// </summary>
    public IAppLogger CreateLogger(string categoryName);

    /// <summary>
    /// 创建一个日志记录器，使用类型作为日志源。
    /// </summary>
    public IAppLogger<T> CreateLogger<T>();
}

/// <summary>
/// 一个自定义的、泛型的日志记录器接口，提供了简洁的日志级别方法。
/// 它的方法与 <see cref="Microsoft.Extensions.Logging.LogLevel"/> 枚举对齐。
/// </summary>
/// <typeparam name="T">日志源的类型上下文。</typeparam>
public interface IAppLogger<T> : IAppLogger;

/// <summary>
/// 一个自定义的日志记录器接口，提供了简洁的日志级别方法。
/// 它的方法与 <see cref="Microsoft.Extensions.Logging.LogLevel"/> 枚举对齐。
/// </summary>
public interface IAppLogger
{
    /// <summary>
    /// 记录 Trace 级别的日志。
    /// <p>包含最详细的信息，通常用于诊断。</p>
    /// </summary>
    public void Trace(
        [StructuredMessageTemplate] string message, params object?[] args
    );

    /// <summary>
    /// 记录 Debug 级别的日志。
    /// <p>用于开发和调试期间的交互式调查。</p>
    /// </summary>
    public void Debug(
        [StructuredMessageTemplate] string message, params object?[] args
    );

    /// <summary>
    /// 记录 Information 级别的日志。
    /// <p>用于追踪应用的常规流程。</p>
    /// </summary>
    public void Info(
        [StructuredMessageTemplate] string message, params object?[] args
    );

    /// <summary>
    /// 记录 Warning 级别的日志。
    /// <p>表示非正常的或意外的事件，但不会导致执行停止。</p>
    /// </summary>
    public void Warn(
        [StructuredMessageTemplate] string message, params object?[] args
    );
#pragma warning disable CA2254
    /// <summary>
    /// 记录 Error 级别的日志。
    /// <p>表示由于失败导致当前执行流程中断。</p>
    /// </summary>
    public sealed void Error(
        [StructuredMessageTemplate] string message, params object?[] args
    ) => this.Error(null, message, args);

    /// <summary>
    /// 记录 Error 级别的日志，并包含异常信息。
    /// <p>表示由于失败导致当前执行流程中断。</p>
    /// </summary>
    public void Error(Exception? exception,
        [StructuredMessageTemplate] string message, params object?[] args
    );

    /// <summary>
    /// 记录 Critical/Fatal 级别的日志。
    /// <p>表示不可恢复的应用程序或系统崩溃，或需要立即关注的灾难性故障。</p>
    /// </summary>
    /// <remarks>
    /// Critical 和 Fatal 互为别名关系，使用完全相同的内部方法。
    /// </remarks>
    public sealed void Critical(
        [StructuredMessageTemplate] string message, params object?[] args
    ) => this.Critical(null, message, args);

    /// <summary>
    /// 记录 Critical/Fatal 级别的日志，并包含异常信息。
    /// <p>表示不可恢复的应用程序或系统崩溃，或需要立即关注的灾难性故障。</p>
    /// </summary>
    /// <remarks>
    /// Critical 和 Fatal 互为别名关系，使用完全相同的内部方法。
    /// </remarks>
    public void Critical(Exception? exception,
        [StructuredMessageTemplate] string message, params object?[] args
    );

    /// <summary>
    /// 记录 Critical/Fatal 级别的日志。
    /// <p>表示不可恢复的应用程序或系统崩溃，或需要立即关注的灾难性故障。</p>
    /// </summary>
    /// <remarks>
    /// Critical 和 Fatal 互为别名关系，使用完全相同的内部方法。
    /// </remarks>
    public sealed void Fatal(
        [StructuredMessageTemplate] string message, params object?[] args
    ) => this.Critical(message, args);

    /// <summary>
    /// 记录 Critical/Fatal 级别的日志，并包含异常信息。
    /// <p>表示不可恢复的应用程序或系统崩溃，或需要立即关注的灾难性故障。</p>
    /// </summary>
    /// <remarks>
    /// Critical 和 Fatal 互为别名关系，使用完全相同的内部方法。
    /// </remarks>
    public sealed void Fatal(Exception exception,
        [StructuredMessageTemplate] string message, params object?[] args
    ) => this.Critical(exception, message, args);
#pragma warning restore CA2254
}