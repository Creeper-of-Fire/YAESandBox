using System.Diagnostics.CodeAnalysis;

namespace YAESandBox.Depend.Results;

/// <summary>
/// 错误的基类
/// </summary>
/// <param name="Message">错误信息。</param>
/// <param name="Exception">可选的内部异常。</param>
public record Error(string Message, Exception? Exception = null)
{
    /// <summary>
    /// 错误信息
    /// </summary>
    internal string Message { get; } = Message;

    /// <summary>
    /// 关联的异常（可能为 null）
    /// </summary>
    internal Exception? Exception { get; init; } = Exception;

    /// <summary>
    /// 从一个 Exception 对象创建一个 Error。错误信息将使用 Exception.Message。
    /// </summary>
    /// <param name="exception">异常对象。</param>
    public Error(Exception exception) : this(exception.Message, exception) { }

    /// <summary>
    /// 转换为 Result
    /// </summary>
    /// <returns></returns>
    public virtual Result ToResult() => this;

    /// <summary>
    /// 转换为 Result&lt;T&gt;
    /// </summary>
    /// <returns></returns>
    public virtual Result<T> ToResult<T>() => this;
}

/// <summary>
/// 错误的扩展方法
/// </summary>
public static class ErrorExtension
{
    /// <summary>
    /// 附加一个异常作为错误的原因，并返回一个新的 Error 实例。
    /// </summary>
    /// <param name="error">错误。</param>
    /// <param name="exception">导致此错误的异常。</param>
    /// <returns>一个包含异常信息的新 Error 实例。</returns>
    public static TError CauseBy<TError>(this TError error, Exception? exception) where TError : Error
    {
        return error with { Exception = exception };
    }
}

/// <summary>
/// Result 到 Task 的转换
/// </summary>
public static class ResultExtensionToTask
{
    /// <summary>
    /// 将 Error 对象包装到一个已完成的、失败状态的 Task&lt;Result&gt; 中。
    /// </summary>
    /// <param name="error">要包装的 Error 对象。</param>
    /// <returns>一个表示异步操作已失败的 Task。</returns>
    public static Task<Result> AsCompletedTask(this Error error)
    {
        return Task.FromResult(error.ToResult());
    }

    /// <summary>
    /// 将 Error 对象包装到一个已完成的、失败状态的 Task&lt;Result&lt;T&gt;&gt; 中。
    /// </summary>
    /// <param name="error">要包装的 Error 对象。</param>
    /// <returns>一个表示异步操作已失败的 Task。</returns>
    public static Task<Result<T>> AsCompletedTask<T>(this Error error)
    {
        return Task.FromResult(error.ToResult<T>());
    }

    /// <summary>
    /// 将 Result 对象包装到一个已完成状态的 Task&lt;Result&gt; 中。
    /// </summary>
    /// <param name="result">要包装的 Result 对象。</param>
    /// <returns>一个表示异步操作已失败的 Task。</returns>
    public static Task<Result> AsCompletedTask(this Result result)
    {
        return Task.FromResult(result);
    }
}

public partial record Result
{
    internal Result(Error? Error)
    {
        this.Error = Error;
    }

    /// <summary>
    /// 错误
    /// </summary>
    protected Error? Error { get; }

    /// <summary>
    /// 获取错误信息
    /// </summary>
    public Exception? ErrorException => this.Error?.Exception;

    /// <summary>
    /// 成功
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public virtual bool IsSuccess => this.Error is null;

    /// <summary>
    /// 失败
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public virtual bool IsFailed => !this.IsSuccess;

    /// <summary>
    /// 尝试从失败的 Result 获取错误。
    /// </summary>
    /// <param name="error">如果获取成功，则输出错误；否则为 null。</param>
    /// <returns>如果获取失败，则返回 true；否则返回 false。</returns>
    public bool TryGetError([MaybeNullWhen(false)] out Error error)
    {
        if (this.IsFailed)
        {
            error = this.Error;
            return true;
        }

        error = null;
        return false;
    }
}

/// <summary>
/// Result
/// </summary>
public partial record Result
{
    /// <summary>
    /// OK
    /// </summary>
    /// <returns></returns>
    public static Result Ok() => new(null);

    /// <summary>
    /// 泛型 OK
    /// </summary>
    /// <param name="value"></param>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public static Result<TValue> Ok<TValue>(TValue value) => new(null, value);

    /// <summary>
    /// 失败
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    public static Result Fail(Error error) => new(error);

    /// <summary>
    /// 失败
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public static Error Fail(string errorMessage, Error error) => new(errorMessage, error.Exception);

    /// <summary>
    /// 失败
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <param name="exception"></param>
    /// <returns></returns>
    public static Error Fail(string errorMessage, Exception? exception = null) => new(errorMessage, exception);

    /// <summary>
    /// 隐式转换
    /// </summary>
    public static implicit operator Result(Error error) => Fail(error);
}

/// <summary>
/// 泛型 Result
/// </summary>
/// <typeparam name="TValue"></typeparam>
public record Result<TValue> : Result
{
    internal Result(Error? Error, TValue? Value) : base(Error)
    {
        this.Value = Value;
    }

    /// <summary>
    /// 映射，失败的部分保持不变，成功的部分改变类型
    /// </summary>
    /// <param name="mapper"></param>
    /// <typeparam name="TNewValue"></typeparam>
    /// <returns></returns>
    public Result<TNewValue> Map<TNewValue>(Func<Result<TValue>, Result<TNewValue>> mapper)
    {
        if (this.IsFailed)
            return new Result<TNewValue>(this.Error, default);

        return mapper(this);
    }

    /// <summary>
    /// 转换为 Result
    /// </summary>
    /// <returns></returns>
    public Result ToResult() => this;

    /// <summary>
    /// 内部存储使用
    /// </summary>
    protected TValue? Value { get; }

    /// <summary>
    /// 尝试从成功的 Result 获取值。
    /// </summary>
    /// <remarks>
    /// 注意：如果 Result 是通过 `Ok(null)` 创建的，即使成功，`value` 也将是 `null`。
    /// 调用者应该根据 TValue 的可空性来处理这种情况。
    /// </remarks>
    /// <param name="value">如果获取成功，则输出结果的值；否则为 default(TValue)。</param>
    /// <returns>如果获取成功，则返回 true；否则返回 false。</returns>
    public bool TryGetValue([MaybeNullWhen(false)] out TValue value)
    {
        if (this.IsSuccess)
        {
            // 我们使用 null-forgiving 操作符 (!) 来告诉编译器，
            // 我们接受将内部的 Value (可能为null) 赋值给 out 参数。
            // 这是为了统一处理 TValue 是可空和不可空类型的情况。
            // ReSharper disable once NullableWarningSuppressionIsUsed
            value = this.Value!;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc cref="TryGetValue(out TValue)"/>
    /// <param name="error">如果Get失败，则输出错误；否则为 null。</param>
#pragma warning disable CS1573 // 参数在 XML 注释中没有匹配的 param 标记(但其他参数有)
    public bool TryGetValue([MaybeNullWhen(false)] out TValue value, [MaybeNullWhen(true)] out Error error)
#pragma warning restore CS1573 // 参数在 XML 注释中没有匹配的 param 标记(但其他参数有)
    {
        error = this.Error;
        return this.TryGetValue(value: out value);
    }

    /// <inheritdoc cref="TryGetError"/>
    /// <param name="value">如果获取成功，则输出结果的值；否则为 default(TValue)。</param>
#pragma warning disable CS1573 // 参数在 XML 注释中没有匹配的 param 标记(但其他参数有)
    public bool TryGetError([MaybeNullWhen(false)] out Error error, [MaybeNullWhen(true)] out TValue value)
#pragma warning restore CS1573 // 参数在 XML 注释中没有匹配的 param 标记(但其他参数有)
    {
        value = this.Value;
        return this.TryGetError(error: out error);
    }

    /// <summary>
    /// 隐式转换
    /// </summary>
    public static implicit operator Result<TValue>(TValue value) => new(null, value);

    /// <summary>
    /// 隐式转换
    /// </summary>
    public static implicit operator Result<TValue>(Error error) => new(error, default);
}