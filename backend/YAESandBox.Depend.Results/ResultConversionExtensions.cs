using System.ComponentModel.DataAnnotations;

namespace YAESandBox.Depend.Results;

/// <summary>
/// 代表一个单个项的成功与否的DTO
/// </summary>
/// <typeparam name="TValue">数值的类型</typeparam>
public record ResultDto<TValue>
{
    /// <summary>
    /// 是否成功
    /// </summary>
    [Required]
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 成功时，这里有一个值
    /// </summary>
    public required TValue? Data { get; init; }

    /// <summary>
    /// 失败时，返回由 Error.ToDetailString() 生成的完整、详细的错误信息。
    /// </summary>
    public string? ErrorDetails { get; init; }
}

/// <summary>
/// 可以转换为SingleItemResultDto或其派生类型的错误
/// </summary>
public interface IErrorCanBeDto
{
    /// <summary>
    /// 转换错误为DTO
    /// </summary>
    /// <returns></returns>
    public ResultDto<TValue> ToDto<TValue>();
}

/// <summary>
/// 提供用于将Result类型转换为DTO的扩展方法。
/// </summary>
public static class ResultConversionExtensions
{
    /// <summary>
    /// 将一个表示独立操作结果的 Result`T 转换为转换为前端友好的 DTO。
    /// </summary>
    /// <typeparam name="T">数据类型。</typeparam>
    /// <param name="result">要转换的Result对象，请确保其是无错的（把错误在外部进行检测）。</param>
    /// <returns>转换后的DTO对象。</returns>
    public static ResultDto<T> ToSingleItemResultDto<T>(this Result<T> result)
    {
        if (result.TryGetValue(out var data, out var error))
        {
            return new ResultDto<T> { IsSuccess = true, Data = data, ErrorDetails = null };
        }

        if (error is IErrorCanBeDto errorCanBeDto)
        {
            return errorCanBeDto.ToDto<T>();
        }

        return new ResultDto<T> { IsSuccess = false, Data = default, ErrorDetails = error.ToDetailString() };
    }

    /// <summary>
    /// 将一个 DictionaryResult`TKey, TValue` 转换为前端友好的字典 DTO。
    /// </summary>
    /// <typeparam name="TKey">字典的键类型。</typeparam>
    /// <typeparam name="TValue">字典的值类型。</typeparam>
    /// <param name="dictionaryResult">要转换的DictionaryResult对象，请确保其是无错的（把错误在外部进行检测）。</param>
    /// <returns>一个以TKey为键，以SingleItemResultDto`TValue`为值的字典。</returns>
    public static Dictionary<TKey, ResultDto<TValue>> ToDictionaryDto<TKey, TValue>(
        this DictionaryResult<TKey, TValue> dictionaryResult) where TKey : notnull
    {
        // 如果整个批量操作成功，但内部可能有个别失败项
        if (dictionaryResult is { IsSuccess: true, ItemResults: not null })
        {
            return dictionaryResult.ItemResults.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToSingleItemResultDto() // 复用单个项的转换逻辑
            );
        }

        // 如果整个批量操作在启动前就失败了，返回一个空字典。
        // Controller层应该已经处理了顶层的Error，所以这里可以安全地返回空。
        return [];
    }

    /// <summary>
    /// 将一个 CollectionResult`TKey, TValue` 转换为前端友好的列表 DTO。
    /// </summary>
    /// <typeparam name="T">列表内容的类型。</typeparam>
    /// <param name="collectionResult">要转换的CollectionResult对象，请确保其是无错的（把错误在外部进行检测）。</param>
    /// <returns>一个列表。</returns>
    public static List<ResultDto<T>> ToListDto<T>(
        this CollectionResult<T> collectionResult)
    {
        if (collectionResult is { IsSuccess: true, ItemResults: not null })
        {
            return collectionResult.ItemResults
                .Select(result => result.ToSingleItemResultDto())
                .ToList();
        }

        return [];
    }
}