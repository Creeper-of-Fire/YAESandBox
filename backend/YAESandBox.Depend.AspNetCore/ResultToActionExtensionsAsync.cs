﻿using Microsoft.AspNetCore.Mvc;
using YAESandBox.Depend.Results;
using static YAESandBox.Depend.AspNetCore.ResultToActionExtensions;

namespace YAESandBox.Depend.AspNetCore;

/// <summary>
/// 提供将 <see cref="Result"/> 对象转换为 ASP.NET Core <see cref="ActionResult"/> 的异步扩展方法。
/// 这些方法旨在简化控制器中处理操作结果并将其映射到适当 HTTP 响应的模式。
/// </summary>
public static class ResultToActionExtensionsAsync
{
    /// <summary>
    /// [异步] 将 Result (非泛型) 从一个 Task 中解包并转换为 ActionResult。
    /// 成功时，返回 NoContentResult (HTTP 204 No Content)。
    /// 失败时，返回包含错误消息的 ObjectResult，它会根据顶层Error返回相应的HTTP错误状态码 (400, 404, 500等)。
    /// 此方法会等待异步操作 <paramref name="taskResult"/> 完成。
    /// </summary>
    /// <param name="taskResult">一个表示异步操作的 Task，其结果为 FluentResults.Result 对象。</param>
    /// <returns>
    /// 一个表示异步转换操作的 Task。
    /// 如果原始异步操作成功，则 Task 的结果为 <see cref="NoContentResult"/>。
    /// 如果原始异步操作失败，则 Task 的结果为包含单个错误消息字符串的 <see cref="ObjectResult"/>，状态码为500。
    /// </returns>
    public static async Task<ActionResult> ToActionResultAsync(this Task<Result> taskResult)
    {
        var result = await taskResult.ConfigureAwait(false); // 等待异步操作完成并获取结果
        return result.ToActionResult(); // 调用相应的同步版本进行转换
    }


    /// <summary>
    /// [异步] 将 Result 从一个 Task 中解包并转换为 ActionResult。
    /// 成功时，返回包含值的 OkObjectResult (HTTP 200 OK)。
    /// 失败时，返回包含错误消息的 ObjectResult，它会根据顶层Error返回相应的HTTP错误状态码 (400, 404, 500等)。
    /// 此方法会等待异步操作 <paramref name="taskResult"/> 完成。
    /// </summary>
    /// <typeparam name="T">结果中值的类型。</typeparam>
    /// <param name="taskResult">一个表示异步操作的 Task，其结果为 FluentResults.Result 对象。</param>
    /// <returns>
    /// 一个表示异步转换操作的 Task。
    /// 如果原始异步操作成功，则 Task 的结果为包含结果值的 <see cref="OkObjectResult"/>。
    /// 如果原始异步操作失败，则 Task 的结果为包含单个错误消息字符串的 <see cref="ObjectResult"/>，状态码为500。
    /// </returns>
    public static async Task<ActionResult<T>> ToActionResultAsync<T>(this Task<Result<T>> taskResult)
    {
        var result = await taskResult.ConfigureAwait(false); // 等待异步操作完成并获取结果
        return result.ToActionResult(); // 调用相应的同步版本进行转换
    }

    /// <summary>
    /// [异步] 将包裹在 Task 中的 DictionaryResult`TKey, TValue` 解包并转换为 ActionResult。
    /// 此方法会等待异步操作完成，然后调用其同步的 ToActionResult 版本进行转换。
    /// </summary>
    /// <typeparam name="TKey">字典的键类型。</typeparam>
    /// <typeparam name="TData">字典的值类型。</typeparam>
    /// <param name="taskDictionaryResult">一个表示异步操作的 Task，其结果为 DictionaryResult 对象。</param>
    /// <returns>一个表示异步转换操作的 Task，其最终结果为相应的 ActionResult。</returns>
    public static async Task<ActionResult<Dictionary<TKey, ResultDto<TData>>>> ToActionResultAsync<TKey, TData>(
        this Task<DictionaryResult<TKey, TData>> taskDictionaryResult) where TKey : notnull
    {
        // 1. 等待异步操作完成，获取 DictionaryResult 实例。
        var dictionaryResult = await taskDictionaryResult.ConfigureAwait(false);

        // 2. 调用相应的同步 ToActionResult 扩展方法。
        return dictionaryResult.ToActionResult();
    }

    /// <inheritdoc cref="ToActionResultAsync{Tkey,TValue}"/>
    public static async Task<ActionResult<Dictionary<TKey, TNewValue>>> ToActionResultAsync<TKey, TData, TNewKey, TNewValue>(
        this Task<DictionaryResult<TKey, TData>> taskDictionaryResult,
        Func<Dictionary<TKey, ResultDto<TData>>, Dictionary<TNewKey, TNewValue>> selector) where TKey : notnull where TNewKey : notnull
    {
        // 1. 等待异步操作完成，获取 DictionaryResult 实例。
        var dictionaryResult = await taskDictionaryResult.ConfigureAwait(false);

        // 2. 调用相应的同步 ToActionResult 扩展方法。
        return dictionaryResult.ToActionResult(selector);
    }

    /// <summary>
    /// [异步] 将包裹在 Task 中的 CollectionResult`T` 解包并转换为 ActionResult。
    /// 此方法会等待异步操作完成，然后调用其同步的 ToActionResult 版本进行转换。
    /// </summary>
    /// <typeparam name="T">集合中元素的类型。</typeparam>
    /// <param name="taskCollectionResult">一个表示异步操作的 Task，其结果为 CollectionResult 对象。</param>
    /// <returns>一个表示异步转换操作的 Task，其最终结果为相应的 ActionResult。</returns>
    public static async Task<ActionResult<List<ResultDto<T>>>> ToActionResultAsync<T>(
        this Task<CollectionResult<T>> taskCollectionResult)
    {
        // 1. 等待异步操作完成，获取 CollectionResult 实例。
        var collectionResult = await taskCollectionResult.ConfigureAwait(false);

        // 2. 调用相应的同步 ToActionResult 扩展方法。
        return collectionResult.ToActionResult();
    }
}