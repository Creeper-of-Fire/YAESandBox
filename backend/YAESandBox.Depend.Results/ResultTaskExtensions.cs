namespace YAESandBox.Depend.Results;

/// <summary>
/// 为 Task&gt;Result&lt; 和 Task&gt;Result&gt;TValue&lt;&lt; 提供流畅的链式调用扩展方法。
/// </summary>
public static class ResultTaskExtensions
{
    // --- Then for Task<Result> ---
    /// <summary>
    /// 如果前一个异步操作成功，则执行下一个同步操作。
    /// </summary>
    /// <param name="task">前一个异步操作。</param>
    /// <param name="onSuccess">如果成功，要执行的同步委托。</param>
    /// <returns>一个新的 Task&gt;Result&lt;，代表整个链式操作的结果。</returns>
    public static async Task<Result> Then(this Task<Result> task, Func<Result> onSuccess)
    {
        Result firstResult = await task.ConfigureAwait(false);
        return firstResult.IsSuccess ? onSuccess() : firstResult;
    }

    /// <summary>
    /// 如果前一个异步操作成功，则执行下一个异步操作。
    /// </summary>
    /// <param name="task">前一个异步操作。</param>
    /// <param name="onSuccessAsync">如果成功，要执行的异步委托。</param>
    /// <returns>一个新的 Task&gt;Result&lt;，代表整个链式操作的结果。</returns>
    public static async Task<Result> Then(this Task<Result> task, Func<Task<Result>> onSuccessAsync)
    {
        Result firstResult = await task.ConfigureAwait(false);
        return firstResult.IsSuccess ? await onSuccessAsync().ConfigureAwait(false) : firstResult;
    }
    
    // --- Then for Task<Result<TValue>> ---
    /// <summary>
    /// 如果前一个异步操作成功，则将其成功值传递给下一个同步操作。
    /// </summary>
    /// <typeparam name="TValue">前一个操作的成功值类型。</typeparam>
    /// <param name="task">前一个异步操作。</param>
    /// <param name="onSuccess">如果成功，要执行的接收 TValue 并返回 Result 的同步委托。</param>
    /// <returns>一个新的 Task&gt;Result&lt;，代表整个链式操作的结果。</returns>
    public static async Task<Result> Then<TValue>(this Task<Result<TValue>> task, Func<TValue, Result> onSuccess)
    {
        Result<TValue> firstResult = await task.ConfigureAwait(false);
        if (firstResult.TryGetValue(out var value, out var error))
        {
            return onSuccess(value);
        }
        return error;
    }
    
    /// <summary>
    /// 如果前一个异步操作成功，则将其成功值传递给下一个异步操作。
    /// </summary>
    /// <typeparam name="TValue">前一个操作的成功值类型。</typeparam>
    /// <param name="task">前一个异步操作。</param>
    /// <param name="onSuccessAsync">如果成功，要执行的接收 TValue 并返回 Task&gt;Result&lt; 的异步委托。</param>
    /// <returns>一个新的 Task&gt;Result&lt;，代表整个链式操作的结果。</returns>
    public static async Task<Result> Then<TValue>(this Task<Result<TValue>> task, Func<TValue, Task<Result>> onSuccessAsync)
    {
        Result<TValue> firstResult = await task.ConfigureAwait(false);
        if (firstResult.TryGetValue(out var value, out var error))
        {
            return await onSuccessAsync(value).ConfigureAwait(false);
        }
        return error;
    }
    
    
    // --- Then for chaining Task<Result<TValue>> to Task<Result<TNewValue>> ---

    /// <summary>
    /// 如果前一个异步操作成功，则将其成功值映射到下一个同步操作，从而改变 Result 的类型。
    /// </summary>
    /// <typeparam name="TValue">前一个操作的成功值类型。</typeparam>
    /// <typeparam name="TNewValue">新操作的成功值类型。</typeparam>
    /// <param name="task">前一个异步操作。</param>
    /// <param name="onSuccess">如果成功，要执行的接收 TValue 并返回 Result&gt;TNewValue&lt; 的同步委托。</param>
    /// <returns>一个新的 Task&gt;Result&gt;TNewValue&lt;&lt;，代表整个链式操作的结果。</returns>
    public static async Task<Result<TNewValue>> Then<TValue, TNewValue>(this Task<Result<TValue>> task, Func<TValue, Result<TNewValue>> onSuccess)
    {
        Result<TValue> firstResult = await task.ConfigureAwait(false);
        if (firstResult.TryGetValue(out var value, out var error))
        {
            return onSuccess(value);
        }
        return error;
    }

    /// <summary>
    /// 如果前一个异步操作成功，则将其成功值映射到下一个异步操作，从而改变 Result 的类型。
    /// </summary>
    /// <typeparam name="TValue">前一个操作的成功值类型。</typeparam>
    /// <typeparam name="TNewValue">新操作的成功值类型。</typeparam>
    /// <param name="task">前一个异步操作。</param>
    /// <param name="onSuccessAsync">如果成功，要执行的接收 TValue 并返回 Task&gt;Result&gt;TNewValue&lt;&lt; 的异步委托。</param>
    /// <returns>一个新的 Task&gt;Result&gt;TNewValue&lt;&lt;，代表整个链式操作的结果。</returns>
    public static async Task<Result<TNewValue>> Then<TValue, TNewValue>(this Task<Result<TValue>> task, Func<TValue, Task<Result<TNewValue>>> onSuccessAsync)
    {
        Result<TValue> firstResult = await task.ConfigureAwait(false);
        if (firstResult.TryGetValue(out var value, out var error))
        {
            return await onSuccessAsync(value).ConfigureAwait(false);
        }
        return error;
    }
}