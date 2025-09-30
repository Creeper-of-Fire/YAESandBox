using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Results;

namespace YAESandBox.Depend.ResultsExtend;

/// <summary>
/// 错误的Helper
/// </summary>
public static class ErrorHelper
{
    /// <inheritdoc cref="IAppLogger.Error(string, object?[])"/>
    public static void Error(this IAppLogger logger, Error error)
    {
        logger.Error("{ErrorDetail}", error.ToDetailString());
    }

    /// <inheritdoc cref="IAppLogger.Critical(string, object?[])"/>
    public static void Critical(this IAppLogger logger, Error error)
    {
        logger.Critical("{ErrorDetail}", error.ToDetailString());
    }

    /// <inheritdoc cref="IAppLogger.Fatal(string, object?[])"/>
    public static void Fatal(this IAppLogger logger, Error error) => Critical(logger, error);

    /// <summary>
    /// 选择成功的 FluentResults.Result
    /// </summary>
    /// <param name="results"></param>
    /// <returns></returns>
    public static IEnumerable<T> SelectSuccessValue<T>(this IEnumerable<Result<T>> results)
    {
        return results.Where(op => op.IsSuccess).SelectNotFailedValue();
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

    // /// <summary>
    // /// 获取成功的结果，并返回一个包含这些结果的列表的Result，同时把Reason部分加入Reasons
    // /// </summary>
    // /// <param name="results"></param>
    // /// <typeparam name="TValue"></typeparam>
    // /// <returns></returns>
    // public static Result<IEnumerable<TValue>> CollectValue<TValue>(this IEnumerable<Result<TValue>> results)
    // {
    //     var resultList = results.ToList();
    //
    //     var finalResult = Result.Ok<IEnumerable<TValue>>(new List<TValue>())
    //         .WithReasons(resultList.SelectMany(result => result.Reasons));
    //
    //     return finalResult.WithValue(resultList.SelectNotFailedValue().ToList());
    // }
}