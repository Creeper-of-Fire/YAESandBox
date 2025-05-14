using FluentResults;
using Microsoft.AspNetCore.Mvc;
using static YAESandBox.Depend.AspNetCore.ResultToActionExtensions;

namespace YAESandBox.Depend.AspNetCore;

/// <summary>
/// 提供将 <see cref="FluentResults.Result"/> 对象转换为 ASP.NET Core <see cref="ActionResult"/> 的异步扩展方法。
/// 这些方法旨在简化控制器中处理操作结果并将其映射到适当 HTTP 响应的模式。
/// </summary>
public static class ResultToActionExtensionsAsync
{
    /// <summary>
    /// [异步] 将 FluentResults.Result 从一个 Task 中解包并转换为 ActionResult。
    /// 成功时，返回包含值的 OkObjectResult (HTTP 200 OK)。
    /// 失败时，返回包含所有错误信息列表（ErrorDto）的 ObjectResult，状态码为 500 Internal Server Error。
    /// 此方法会等待异步操作 <paramref name="taskResult"/> 完成。
    /// </summary>
    /// <typeparam name="T">结果中值的类型。</typeparam>
    /// <param name="taskResult">一个表示异步操作的 Task，其结果为 FluentResults.Result 对象。</param>
    /// <returns>
    /// 一个表示异步转换操作的 Task。
    /// 如果原始异步操作成功，则 Task 的结果为包含结果值的 <see cref="OkObjectResult"/>。
    /// 如果原始异步操作失败，则 Task 的结果为包含错误信息列表的 <see cref="ObjectResult"/>，状态码为500。
    /// </returns>
    public static async Task<ActionResult<T>> ToActionResultDetailedAsync<T>(this Task<Result<T>> taskResult)
    {
        var result = await taskResult.ConfigureAwait(false); // 等待异步操作完成并获取结果
        return result.ToActionResultDetailed(); // 调用相应的同步版本进行转换
    }

    /// <summary>
    /// [异步] 将 FluentResults.Result (非泛型) 从一个 Task 中解包并转换为 ActionResult。
    /// 成功时，返回 NoContentResult (HTTP 204 No Content)。
    /// 失败时，返回包含所有错误信息列表（ErrorDto）的 ObjectResult，状态码为 500 Internal Server Error。
    /// 此方法会等待异步操作 <paramref name="taskResult"/> 完成。
    /// </summary>
    /// <param name="taskResult">一个表示异步操作的 Task，其结果为 FluentResults.Result 对象。</param>
    /// <returns>
    /// 一个表示异步转换操作的 Task。
    /// 如果原始异步操作成功，则 Task 的结果为 <see cref="NoContentResult"/>。
    /// 如果原始异步操作失败，则 Task 的结果为包含错误信息列表的 <see cref="ObjectResult"/>，状态码为500。
    /// </returns>
    public static async Task<ActionResult> ToActionResultDetailedAsync(this Task<Result> taskResult)
    {
        var result = await taskResult.ConfigureAwait(false); // 等待异步操作完成并获取结果
        return result.ToActionResultDetailed(); // 调用相应的同步版本进行转换
    }

    /// <summary>
    /// [异步] 将 FluentResults.Result 从一个 Task 中解包并转换为 ActionResult。
    /// 成功时，返回包含值的 OkObjectResult (HTTP 200 OK)。
    /// 失败时，返回包含第一个错误消息（或默认消息）的 ObjectResult，状态码为 500 Internal Server Error。
    /// 此方法会等待异步操作 <paramref name="taskResult"/> 完成。
    /// </summary>
    /// <typeparam name="T">结果中值的类型。</typeparam>
    /// <param name="taskResult">一个表示异步操作的 Task，其结果为 FluentResults.Result 对象。</param>
    /// <param name="defaultMessage">如果结果失败且没有错误消息，则使用的默认错误消息。</param>
    /// <returns>
    /// 一个表示异步转换操作的 Task。
    /// 如果原始异步操作成功，则 Task 的结果为包含结果值的 <see cref="OkObjectResult"/>。
    /// 如果原始异步操作失败，则 Task 的结果为包含单个错误消息字符串的 <see cref="ObjectResult"/>，状态码为500。
    /// </returns>
    public static async Task<ActionResult<T>> ToActionResultAsync<T>(this Task<Result<T>> taskResult,
        string defaultMessage = DefaultErrorMessage)
    {
        var result = await taskResult.ConfigureAwait(false); // 等待异步操作完成并获取结果
        return result.ToActionResult(defaultMessage); // 调用相应的同步版本进行转换
    }

    /// <summary>
    /// [异步] 将 FluentResults.Result (非泛型) 从一个 Task 中解包并转换为 ActionResult。
    /// 成功时，返回 NoContentResult (HTTP 204 No Content)。
    /// 失败时，返回包含第一个错误消息（或默认消息）的 ObjectResult，状态码为 500 Internal Server Error。
    /// 此方法会等待异步操作 <paramref name="taskResult"/> 完成。
    /// </summary>
    /// <param name="taskResult">一个表示异步操作的 Task，其结果为 FluentResults.Result 对象。</param>
    /// <param name="defaultMessage">如果结果失败且没有错误消息，则使用的默认错误消息。</param>
    /// <returns>
    /// 一个表示异步转换操作的 Task。
    /// 如果原始异步操作成功，则 Task 的结果为 <see cref="NoContentResult"/>。
    /// 如果原始异步操作失败，则 Task 的结果为包含单个错误消息字符串的 <see cref="ObjectResult"/>，状态码为500。
    /// </returns>
    public static async Task<ActionResult> ToActionResultAsync(this Task<Result> taskResult,
        string defaultMessage = DefaultErrorMessage)
    {
        var result = await taskResult.ConfigureAwait(false); // 等待异步操作完成并获取结果
        return result.ToActionResult(defaultMessage); // 调用相应的同步版本进行转换
    }
}