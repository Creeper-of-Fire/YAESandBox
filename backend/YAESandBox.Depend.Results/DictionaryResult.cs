using System.Diagnostics.CodeAnalysis;

namespace YAESandBox.Depend.Results;

/// <summary>
/// 表示对一个字典或键值对集合进行批量操作的结果。
/// 它既能表示整个批量操作本身的成功或失败，
/// 也能包含每个独立键值对的操作结果。
/// </summary>
/// <typeparam name="TKey">字典的键类型。</typeparam>
/// <typeparam name="TValue">字典的值类型。</typeparam>
public record DictionaryResult<TKey, TValue> : Result where TKey : notnull
{
    /// <inheritdoc />
    /// <remarks>当成功时，<see cref="ItemResults"/> 必定有值。</remarks>
    [MemberNotNullWhen(true, nameof(ItemResults))]
    public override bool IsSuccess => base.IsSuccess;

    /// <inheritdoc />
    /// <remarks>当失败时，<see cref="ItemResults"/> 必定为 null。</remarks>
    [MemberNotNullWhen(false, nameof(ItemResults))]
    public override bool IsFailed => base.IsFailed;

    /// <summary>
    /// 包含每个独立键值对操作结果的字典。
    /// 只有当整个批量操作本身成功启动时，这个字典才会有意义。
    /// </summary>
    internal IReadOnlyDictionary<TKey, Result<TValue>>? ItemResults { get; }

    // --- 工厂方法 (Factory Methods) ---

    // 内部构造函数
    private DictionaryResult(Error? error, IReadOnlyDictionary<TKey, Result<TValue>>? itemResults) : base(error)
    {
        this.ItemResults = itemResults;
    }

    /// <summary>
    /// 创建一个表示整个批量操作成功的 DictionaryResult。
    /// </summary>
    /// <param name="itemResults">每个独立键值对的操作结果字典。</param>
    public static DictionaryResult<TKey, TValue> Ok(IReadOnlyDictionary<TKey, Result<TValue>> itemResults)
    {
        return new DictionaryResult<TKey, TValue>(null, itemResults);
    }

    /// <summary>
    /// 创建一个表示整个批量操作在启动前就失败的 DictionaryResult。
    /// </summary>
    /// <param name="error">描述批量操作失败原因的错误。</param>
    public new static DictionaryResult<TKey, TValue> Fail(Error error)
    {
        return new DictionaryResult<TKey, TValue>(error, null);
    }

    // --- 好用的转换和辅助方法 ---

    /// <summary>
    /// 隐式转换
    /// </summary>
    public static implicit operator DictionaryResult<TKey, TValue>(Dictionary<TKey, Result<TValue>> itemResults)
    {
        return Ok(itemResults);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="selector"></param>
    /// <typeparam name="TNewValue"></typeparam>
    /// <returns></returns>
    public DictionaryResult<TKey, TNewValue> Select<TNewValue>(Func<Result<TValue>, Result<TNewValue>> selector)
    {
        if (this.IsFailed)
            return DictionaryResult<TKey, TNewValue>.Fail(this.Error);
        return this.ItemResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Map(selector)).ToDictionaryResult();
    }

    /// <summary>
    /// 获取所有成功项，以键值对的形式返回。
    /// </summary>
    public IEnumerable<KeyValuePair<TKey, TValue>> GetSuccessData()
    {
        if (this.ItemResults == null) yield break;

        foreach (var kvp in this.ItemResults)
        {
            if (kvp.Value.TryGetValue(out var value))
            {
                yield return new KeyValuePair<TKey, TValue>(kvp.Key, value);
            }
        }
    }

    /// <summary>
    /// 将所有成功项转换为一个新的字典。
    /// </summary>
    public Dictionary<TKey, TValue> ToSuccessDictionary()
    {
        return this.GetSuccessData().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// 检查是否有任何一个独立项的操作失败了。
    /// </summary>
    public bool HasAnyItemFailure()
    {
        return this.ItemResults?.Values.Any(r => r.IsFailed) ?? false;
    }
}

/// <summary>
/// 拓展方法，用于将字典结果转换为字典结果对象。
/// </summary>
public static class DictionaryResultExtension
{
    /// <summary>
    /// 将字典结果转换为字典结果对象。
    /// </summary>
    public static DictionaryResult<TKey, TValue> ToDictionaryResult<TKey, TValue>(
        this IReadOnlyDictionary<TKey, Result<TValue>> itemResults)
        where TKey : notnull
    {
        return DictionaryResult<TKey, TValue>.Ok(itemResults);
    }
}