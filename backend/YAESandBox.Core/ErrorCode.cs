using System.Diagnostics.CodeAnalysis;
using FluentResults;
using YAESandBox.Depend;

namespace YAESandBox.Core;

public abstract record LazyInitError(string Message) : IError
{
    public Result ToResult()
    {
        return Result.Fail(this);
    }

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

    public static implicit operator Result(LazyInitError initError)
    {
        return initError.ToResult();
    }
}

public abstract record LazyInitHandledIssue(string Message) : IHandledIssue
{
    public Result ToResult()
    {
        return Result.Ok().WithReason(this);
    }

    public Result<T> ToResult<T>()
    {
        return Result.Ok().WithReason(this);
    }

    /// <summary>
    /// 元数据
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    public Dictionary<string, object> Metadata => field ??= [];

    /// <summary>
    /// 原因
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    public List<IReason> Reasons => field ??= [];
}

public record NormalHandledIssue(BlockResultCode Code, string Message) : LazyInitHandledIssue(Message)
{
    public static NormalHandledIssue NotFound(string message)
    {
        return new NormalHandledIssue(BlockResultCode.NotFound, message);
    }

    public static NormalHandledIssue Conflict(string message)
    {
        return new NormalHandledIssue(BlockResultCode.Conflict, message);
    }

    public static NormalHandledIssue InvalidInput(string message)
    {
        return new NormalHandledIssue(BlockResultCode.InvalidInput, message);
    }

    public static NormalHandledIssue Error(string message)
    {
        return new NormalHandledIssue(BlockResultCode.Error, message);
    }

    public static NormalHandledIssue InvalidState(string message)
    {
        return new NormalHandledIssue(BlockResultCode.InvalidState, message);
    }
}

public record NormalError(BlockResultCode Code, string Message) : LazyInitError(Message)
{
    public static NormalError NotFound(string message)
    {
        return new NormalError(BlockResultCode.NotFound, message);
    }

    public static NormalError Conflict(string message)
    {
        return new NormalError(BlockResultCode.Conflict, message);
    }

    public static NormalError InvalidInput(string message)
    {
        return new NormalError(BlockResultCode.InvalidInput, message);
    }

    public static NormalError Error(string message)
    {
        return new NormalError(BlockResultCode.Error, message);
    }

    public static NormalError InvalidState(string message)
    {
        return new NormalError(BlockResultCode.InvalidState, message);
    }
}

/// <summary>
/// 错误已经被处理，但是相关的信息依旧需要得到保留
/// </summary>
public interface IHandledIssue : IReason
{
    public List<IReason> Reasons { get; }
}

public static class ErrorHelper
{
    public static IEnumerable<IHandledIssue> HandledIssue(this IResultBase resultBase)
    {
        return resultBase.Reasons.OfType<IHandledIssue>();
    }

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
        return results.Where(op => !op.Errors.Any() && !op.HandledIssue().Any()).Select(op => op.Value);
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

        finalResult.WithValue(resultList.Select(r => r.Value).ToList());

        return finalResult;
    }
}