using System.Diagnostics.CodeAnalysis;
using FluentResults;

namespace YAESandBox.Depend.Results;

/// <summary>
/// 方便的已解决问题的创建，使用了懒加载提高性能/减少内存使用
/// </summary>
/// <param name="Message"></param>
public abstract record LazyInitHandledIssue(string Message) : IHandledIssue
{
    /// <summary>转为Result</summary>
    public Result ToResult()
    {
        return Result.Ok().WithReason(this);
    }

    /// <summary>转为Result</summary>
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

/// <summary>
/// 错误已经被处理，但是相关的信息依旧需要得到保留
/// </summary>
public interface IHandledIssue : IReason
{
    /// <summary>
    /// 内部的“形成这个问题的原因列表”
    /// </summary>
    public List<IReason> Reasons { get; }
}