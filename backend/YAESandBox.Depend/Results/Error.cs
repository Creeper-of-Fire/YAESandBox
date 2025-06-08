using System.Diagnostics.CodeAnalysis;
using FluentResults;

namespace YAESandBox.Depend.Results;

/// <summary>
/// 方便的错误创建，使用了懒加载提高性能/减少内存使用
/// </summary>
/// <param name="Message"></param>
public abstract record LazyInitError(string Message) : IError
{
    /// <summary>转为Result</summary>
    public Result ToResult()
    {
        return Result.Fail(this);
    }

    /// <summary>转为Result</summary>
    public Result<T> ToResult<T>()
    {
        return Result.Fail(this);
    }

    /// <summary>
    /// 元数据
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    public Dictionary<string, object> Metadata => field ??= [];

    /// <summary>
    /// 错误原因
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    public List<IError> Reasons => field ??= [];

    /// <summary>
    /// 自动转换
    /// </summary>
    /// <param name="initError"></param>
    /// <returns></returns>
    public static implicit operator Result(LazyInitError initError)
    {
        return initError.ToResult();
    }
}

/// <summary>
/// 错误的Helper
/// </summary>
public static class ErrorHelper
{
    /// <summary>
    /// 从<paramref name="resultBase"/>的Reasons里获取所有IHandledIssue
    /// </summary>
    /// <param name="resultBase"></param>
    /// <returns></returns>
    public static IEnumerable<IHandledIssue> HandledIssue(this IResultBase resultBase)
    {
        return resultBase.Reasons.OfType<IHandledIssue>();
    }

    /// <summary>
    /// 从<paramref name="resultBase"/>的Reasons中检测是否存在IHandledIssue
    /// </summary>
    /// <param name="resultBase"></param>
    /// <typeparam name="THandledIssue"></typeparam>
    /// <returns></returns>
    public static bool HasHandledIssue<THandledIssue>(this IResultBase resultBase) where THandledIssue : IHandledIssue
    {
        return resultBase.HandledIssue().OfType<THandledIssue>().Any();
    }

    /// <summary>
    /// 选择成功的 FluentResults.Result
    /// </summary>
    /// <param name="results"></param>
    /// <returns></returns>
    public static IEnumerable<T> SelectSuccessValue<T>(this IEnumerable<Result<T>> results)
    {
        return results.Where(op => !op.Errors.Any() && !op.HandledIssue().Any()).SelectNotFailedValue();
    }

    /// <summary>
    /// 实际上类似于results.Select(op => op.Value)
    /// </summary>
    /// <param name="results"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private static IEnumerable<T> SelectNotFailedValue<T>(this IEnumerable<Result<T>> results)
    {
        foreach (var op in results)
        {
            var op1 = op.TryGetValue(out var value) ? value : default;
            if (op1 is not null)
                yield return op1;
        }
    }

    // /// <summary>
    // /// 使用FluentResults.Result.Merge，合并 Result 列表 为一个Result。
    // /// </summary>
    // /// <param name="results"></param>
    // /// <typeparam name="T"></typeparam>
    // /// <returns></returns>
    // public static Result MergeListResults<T>(this IEnumerable<Result<T>> results) =>
    //     Result.Merge(results.ToArray()).ToResult();
    //
    // /// <summary>
    // /// 使用FluentResults.Result.Merge，合并 Result 列表 为一个 Result 泛型。
    // /// </summary>
    // /// <param name="results"></param>
    // /// <typeparam name="T"></typeparam>
    // /// <returns></returns>
    // public static Result<IEnumerable<T>> MergeListResultsToList<T>(this IEnumerable<Result<T>> results) =>
    //     Result.Merge(results.ToArray());

    /// <summary>
    /// 获取成功的结果，并返回一个包含这些结果的列表的Result，同时把Reason部分加入Reasons
    /// </summary>
    /// <param name="results"></param>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public static Result<IEnumerable<TValue>> CollectValue<TValue>(this IEnumerable<Result<TValue>> results)
    {
        var resultList = results.ToList();

        var finalResult = Result.Ok<IEnumerable<TValue>>(new List<TValue>())
            .WithReasons(resultList.SelectMany(result => result.Reasons));

        return finalResult.WithValue(resultList.SelectNotFailedValue().ToList());
    }
}