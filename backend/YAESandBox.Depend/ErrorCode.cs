using System.Diagnostics.CodeAnalysis;
using FluentResults;

namespace YAESandBox.Depend;

public enum ResultCode
{
    /// <summary>
    /// 成功
    /// </summary>
    Success,

    /// <summary>
    /// 目标未找到
    /// </summary>
    NotFound,

    /// <summary>
    /// 输入无效
    /// </summary>
    InvalidInput,

    /// <summary>
    /// 状态无效
    /// </summary>
    InvalidState,

    /// <summary>
    /// 未授权
    /// </summary>
    Unauthorized,

    /// <summary>
    /// 禁止
    /// </summary>
    Forbidden,

    /// <summary>
    /// 循环操作
    /// </summary>
    CyclicOperation,

    /// <summary>
    /// 冲突
    /// </summary>
    Conflict,

    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// 不支持
    /// </summary>
    NotSupported,

    /// <summary>
    /// 超时
    /// </summary>
    Timeout,

    /// <summary>
    /// 未定义，可能出错可能没有出错，可能成功可能没有成功，可能既没有成功也没有失败
    /// </summary>
    Undefined
}

/// <summary>
/// 表示原子操作执行结果的枚举。
/// </summary>
public enum AtomicExecutionResult
{
    /// <summary>
    /// 操作已成功执行 (通常在 Idle 状态下)。
    /// </summary>
    Executed,

    /// <summary>
    /// 操作已成功执行并且/或者已暂存 (通常在 Loading 状态下)。
    /// </summary>
    ExecutedAndQueued,

    /// <summary>
    /// 目标 Block 未找到。
    /// </summary>
    NotFound,

    /// <summary>
    /// Block 当前处于冲突状态，无法执行操作。
    /// </summary>
    ConflictState,

    /// <summary>
    /// 执行过程中发生错误。
    /// </summary>
    Error
}

/// <summary>
/// 表示管理操作的通用结果枚举。
/// 包含来自 DeleteResult 和 MoveResult 的可能状态，以及创建操作的状态。
/// </summary>
public enum ManagementResult
{
    /// <summary>
    /// 成功
    /// </summary>
    Success,

    /// <summary>
    /// 目标或者父 Block 不存在
    /// </summary>
    NotFound,

    /// <summary>
    /// 根 Block 无法被操作
    /// </summary>
    CannotPerformOnRoot,

    /// <summary>
    /// 状态不允许操作
    /// </summary>
    InvalidState,

    /// <summary>
    /// 循环操作，如移动 Block 为其子 Block
    /// </summary>
    CyclicOperation,

    /// <summary>
    /// 请求错误
    /// </summary>
    BadRequest,

    /// <summary>
    /// 内部错误
    /// </summary>
    Error
}

public abstract record LazyInitError(string Message) : IError
{
    public Result ToResult() => Result.Fail(this);
    public Result<T> ToResult<T>() => Result.Fail(this);

    /// <summary>
    /// 元数据
    /// </summary>
    [field: AllowNull, MaybeNull]
    public Dictionary<string, object> Metadata => field ??= [];

    /// <summary>
    /// 错误原因
    /// </summary>
    [field: AllowNull, MaybeNull]
    public List<IError> Reasons => field ??= [];
}

public abstract record LazyInitWarning(string Message) : IWarning
{
    public Result ToResult() => Result.Ok().WithReason(this);
    public Result<T> ToResult<T>() => Result.Ok().WithReason(this);

    /// <summary>
    /// 元数据
    /// </summary>
    [field: AllowNull, MaybeNull]
    public Dictionary<string, object> Metadata => field ??= [];

    /// <summary>
    /// 原因
    /// </summary>
    [field: AllowNull, MaybeNull]
    public List<IReason> Reasons => field ??= [];
}

public record NormalWarning(ResultCode Code, string Message) : LazyInitWarning(Message)
{
    public static NormalWarning NotFound(string message) => new(ResultCode.NotFound, message);
    public static NormalWarning Conflict(string message) => new(ResultCode.Conflict, message);
    public static NormalWarning InvalidInput(string message) => new(ResultCode.InvalidInput, message);
    public static NormalWarning Error(string message) => new(ResultCode.Error, message);
    public static NormalWarning InvalidState(string message) => new(ResultCode.InvalidState, message);
}

public interface IWarning : IReason
{
    public List<IReason> Reasons { get; }
}

public static class ErrorHelper
{
    public static IEnumerable<IWarning> Warning(this IResultBase resultBase) =>
        resultBase.Reasons.OfType<IWarning>();

    public static bool HasWarning<TWarning>(this IResultBase resultBase) where TWarning : IWarning =>
        resultBase.Warning().OfType<TWarning>().Any();

    /// <summary>
    /// 选择成功的 FluentResults.Result
    /// </summary>
    /// <param name="results"></param>
    /// <returns></returns>
    public static IEnumerable<T> SelectSuccessValue<T>(this IEnumerable<Result<T>> results) =>
        results.Where(op => !op.Errors.Any() && !op.Warning().Any()).Select(op => op.Value);

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