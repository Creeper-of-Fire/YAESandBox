namespace YAESandBox.Depend.Results;

/// <summary>
/// 表示对一个集合进行批量操作的结果。
/// 这个类型既能表示整个批量操作本身的成功或失败，
/// 也能包含集合中每个独立项的操作结果。
/// </summary>
/// <typeparam name="T">集合中元素的类型。</typeparam>
public record CollectionResult<T> : Result
{
    /// <summary>
    /// 包含集合中每个独立项操作结果的列表。
    /// 只有当整个批量操作本身成功启动时，这个列表才会有意义。
    /// 如果批量操作在启动前就失败了，这个列表可能为 null 或空。
    /// </summary>
    internal IReadOnlyList<Result<T>>? ItemResults { get; }

    // 内部构造函数
    private CollectionResult(Error? error, IReadOnlyList<Result<T>>? itemResults) : base(error)
    {
        this.ItemResults = itemResults;
    }

    // --- 工厂方法 (Factory Methods) ---

    /// <summary>
    /// 创建一个表示整个批量操作成功的 CollectionResult。
    /// </summary>
    /// <param name="itemResults">每个独立项的操作结果列表。</param>
    public static CollectionResult<T> Ok(IReadOnlyList<Result<T>> itemResults)
    {
        return new CollectionResult<T>(null, itemResults);
    }

    /// <summary>
    /// 创建一个表示整个批量操作在启动前就失败的 CollectionResult。
    /// </summary>
    /// <param name="error">描述批量操作失败原因的错误。</param>
    public new static CollectionResult<T> Fail(Error error)
    {
        // 批量操作失败时，ItemResults 为 null
        return new CollectionResult<T>(error, null);
    }

    // --- 好用的转换和辅助方法 ---

    /// <summary>
    /// 隐式转换
    /// </summary>
    public static implicit operator CollectionResult<T>(List<Result<T>> itemResults)
    {
        return Ok(itemResults);
    }

    /// <summary>
    /// 获取所有成功项的数据。
    /// </summary>
    public IEnumerable<T> GetSuccessData()
    {
        if (this.ItemResults is null) yield break;

        foreach (var result in this.ItemResults)
        {
            if (result.TryGetValue(out var value))
            {
                yield return value;
            }
        }
    }

    /// <summary>
    /// 检查是否有任何一个独立项的操作失败了（即使整个批量操作是成功的）。
    /// </summary>
    public bool HasAnyItemFailure()
    {
        return this.ItemResults?.Any(r => r.IsFailed) ?? false;
    }

    /// <summary>
    /// 获取所有Item失败项的错误信息。
    /// </summary>
    public IEnumerable<Error> GetAllItemErrors()
    {
        if (this.ItemResults is null) yield break;

        foreach (var result in this.ItemResults)
        {
            if (result.TryGetError(out var error))
            {
                yield return error;
            }
        }
    }
}

/// <summary>
/// 
/// </summary>
public static class CollectionResultExtension
{
    /// <summary>
    /// 把结果列表转换成CollectionResult
    /// </summary>
    /// <param name="values"></param>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public static CollectionResult<TValue> ToCollectionResult<TValue>(this IReadOnlyList<Result<TValue>> values)
    {
        return CollectionResult<TValue>.Ok(values);
    }
}