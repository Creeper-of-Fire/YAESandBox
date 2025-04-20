using FluentResults;
using OneOf;

namespace YAESandBox.Core.Block;

public static class OneOfExtensions
{
    public static OneOf<T0> ToOneOf<T0>(T0 source)
    {
        return source;
    }
    
    /// <summary>
    /// 将 T0 拓宽为包含更多类型的目标 OneOf 类型 TTarget。
    /// </summary>
    public static OneOf<T0, TTarget> Widen<T0, TTarget>(this OneOf<T0> source)
    {
        return source.Match<OneOf<T0, TTarget>>(
            t0 => t0
        );
    }

    /// <summary>
    /// 将 T0, T1 拓宽为包含更多类型的目标 OneOf 类型 TTarget。
    /// 假定 TTarget 包含了 T0 和 T1 作为其可能的类型。
    /// </summary>
    public static OneOf<T0, T1, TTarget> Widen<T0, T1, TTarget>(this OneOf<T0, T1> source)
        // 理论上我们希望约束 TTarget 必须是 OneOf<..., T0, ..., T1, ...>
        // 但 C# 泛型约束做不到这一点，我们依赖 OneOf 内部的隐式转换能力
        // 或者更简单，不加约束，依赖 Match 内部转换
    {
        // 利用 Match 和 OneOf 从基础类型 T0/T1 到目标 TTarget 的隐式转换
        return source.Match<OneOf<T0, T1, TTarget>>(
            t0 => t0, // 如果 TTarget 包含 T0，这里隐式转换 T0 -> TTarget
            t1 => t1 // 如果 TTarget 包含 T1，这里隐式转换 T1 -> TTarget
        );
        // 如果 TTarget 不包含 T0 或 T1，这会在运行时 Match 方法内部失败（抛异常或返回默认？）
        // OneOf 的设计通常会确保这种隐式转换是有效的。
    }

    /// <summary>
    /// 将 T0, T1, T2 拓宽为包含更多类型的目标 OneOf 类型 TTarget。
    /// </summary>
    public static OneOf<T0, T1, T2, TTarget> Widen<T0, T1, T2, TTarget>(this OneOf<T0, T1, T2> source)
    {
        return source.Match<OneOf<T0, T1, T2, TTarget>>(
            t0 => t0,
            t1 => t1,
            t2 => t2
        );
    }

    /// <summary>
    /// 将 T0, T1, T2, T3 拓宽为包含更多类型的目标 OneOf 类型 TTarget。
    /// </summary>
    public static OneOf<T0, T1, T2, T3, TTarget> Widen<T0, T1, T2, T3, TTarget>(this OneOf<T0, T1, T2, T3> source)
    {
        return source.Match<OneOf<T0, T1, T2, T3, TTarget>>(
            t0 => t0,
            t1 => t1,
            t2 => t2,
            t3 => t3
        );
    }


    public static Result<OneOf<T0, TTarget>> Widen<T0, TTarget>(this Result<OneOf<T0>> source) =>
        source.Map<OneOf<T0, TTarget>>(s => s.Widen<T0, TTarget>());

    public static Result<OneOf<T0, T1, TTarget>> Widen<T0, T1, TTarget>(this Result<OneOf<T0, T1>> source) =>
        source.Map<OneOf<T0, T1, TTarget>>(s => s.Widen<T0, T1, TTarget>());

    public static Result<OneOf<T0, T1, T2, TTarget>>
        Widen<T0, T1, T2, TTarget>(this Result<OneOf<T0, T1, T2>> source) =>
        source.Map<OneOf<T0, T1, T2, TTarget>>(s => s.Widen<T0, T1, T2, TTarget>());

    public static Result<OneOf<T0, T1, T2, T3, TTarget>> Widen<T0, T1, T2, T3, TTarget>(
        this Result<OneOf<T0, T1, T2, T3>> source) =>
        source.Map<OneOf<T0, T1, T2, T3, TTarget>>(s => s.Widen<T0, T1, T2, T3, TTarget>());


    public static TResult ForceResult<T0, T1, TTarget, TResult>(this OneOf<T0, T1> source,
        Func<TTarget, TResult> action)
        where T0 : TTarget where T1 : TTarget
    {
        return source.Match(
            t0 => action(t0),
            t1 => action(t1)
        );
    }

    public static TResult ForceResult<T0, T1, T2, TTarget, TResult>(this OneOf<T0, T1, T2> source,
        Func<TTarget, TResult> action)
        where T0 : TTarget where T1 : TTarget where T2 : TTarget
    {
        return source.Match(
            t0 => action(t0),
            t1 => action(t1),
            t2 => action(t2)
        );
    }

    public static TResult ForceResult<T0, T1, T2, T3, TTarget, TResult>(this OneOf<T0, T1, T2, T3> source,
        Func<TTarget, TResult> action)
        where T0 : TTarget where T1 : TTarget where T2 : TTarget where T3 : TTarget
    {
        return source.Match(
            t0 => action(t0),
            t1 => action(t1),
            t2 => action(t2),
            t3 => action(t3)
        );
    }

    public static TResult ForceResult<T0, T1, T2, T3, T4, TTarget, TResult>(this OneOf<T0, T1, T2, T3, T4> source,
        Func<TTarget, TResult> action)
        where T0 : TTarget where T1 : TTarget where T2 : TTarget where T3 : TTarget where T4 : TTarget
    {
        return source.Match(
            t0 => action(t0),
            t1 => action(t1),
            t2 => action(t2),
            t3 => action(t3),
            t4 => action(t4)
        );
    }

    // 可以根据需要为更多数量的类型参数添加重载...
}
