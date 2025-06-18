using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using static YAESandBox.Depend.ResultsExtend.NormalError;

namespace YAESandBox.Depend.AspNetCore;

public static partial class ResultToActionExtensions
{
    /// <summary>
    /// 智能地将 Result<typeparamref name="T"/> 转换为 ActionResult<typeparamref name="T"/>。
    /// 成功时，返回包含值的 OkObjectResult (HTTP 200 OK)。
    /// 失败时，检查错误：
    /// - 如果错误是 <see cref="NormalError"/> 类型并包含有效的 <see cref="NormalError.ServerErrorType"/> Code，
    ///   则根据该 Code 映射到相应的 HTTP 状态码，并返回包含错误消息的 ObjectResult。
    /// - 否则，返回包含错误消息的 ObjectResult，状态码为 500 Internal Server Error。
    /// </summary>
    /// <typeparam name="T">结果中值的类型。</typeparam>
    /// <param name="result">要转换的 Result<typeparamref name="T"/> 对象。</param>
    /// <returns>
    /// 成功时为 <see cref="OkObjectResult"/>；
    /// 失败时为 <see cref="ObjectResult"/>，其状态码根据错误类型智能决定。
    /// </returns>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        if (result.TryGetValue(out var value, out var error)) // 尝试获取成功的结果值
            return new OkObjectResult(value); // 成功，返回 200 OK 和值

        // 处理失败情况
        string errorMessage = error.Message;

        if (ServerErrorToActionResult(error, errorMessage, out var actionResult))
            return actionResult;

        // 传统流程/默认行为：如果不是 NormalError 或没有特定 Code，返回 500
        return new ObjectResult(errorMessage) { StatusCode = StatusCodes.Status500InternalServerError };
    }

    /// <summary>
    /// 智能地将 Result (非泛型) 转换为 ActionResult。
    /// 成功时，返回 NoContentResult (HTTP 204 No Content)。
    /// 失败时，检查错误：
    /// - 如果错误是 <see cref="NormalError"/> 类型并包含有效的 <see cref="NormalError.ServerErrorType"/> Code，
    ///   则根据该 Code 映射到相应的 HTTP 状态码，并返回包含错误消息的 ObjectResult。
    /// - 否则，返回包含错误消息的 ObjectResult，状态码为 500 Internal Server Error。
    /// </summary>
    /// <param name="result">要转换的 Result 对象。</param>
    /// <returns>
    /// 成功时为 <see cref="NoContentResult"/>；
    /// 失败时为 <see cref="ObjectResult"/>，其状态码根据错误类型智能决定。
    /// </returns>
    public static ActionResult ToActionResult(this Result result)
    {
        if (!result.TryGetError(out var error)) // 检查结果是否成功
            return new NoContentResult(); // 成功，返回 204 No Content

        // 处理失败情况
        string errorMessage = error.Message;

        if (ServerErrorToActionResult(error, errorMessage, out var actionResult))
            return actionResult;

        // 传统流程/默认行为：如果不是 NormalError 或没有特定 Code，返回 500
        return new ObjectResult(errorMessage) { StatusCode = StatusCodes.Status500InternalServerError };
    }

    /// <summary>
    /// 智能地将 DictionaryResult`TKey, TValue` 转换为 ActionResult。
    /// - 如果整个批量操作失败，它会根据顶层Error返回相应的HTTP错误状态码 (400, 404, 500等)。
    /// - 如果整个批量操作成功，它会将内部的字典转换为DTO，并用 200 OK 返回。
    /// </summary>
    /// <typeparam name="TKey">字典的键类型。</typeparam>
    /// <typeparam name="TValue">字典的值类型。</typeparam>
    /// <param name="dictionaryResult">要转换的DictionaryResult对象。</param>
    /// <returns>一个封装了操作结果的ActionResult。</returns>
    public static ActionResult<Dictionary<TKey, SingleItemResultDto<TValue>>> ToActionResult<TKey, TValue>(
        this DictionaryResult<TKey, TValue> dictionaryResult) where TKey : notnull
    {
        // 1. 检查整个批量操作是否在启动前就失败了。
        if (dictionaryResult.TryGetError(out var error))
        {
            return error.ToResult().ToActionResult();
        }

        // 2. 如果批量操作本身是成功的，则将内部的详细结果转换为DTO。
        // 无论内部是否有单独的项失败，整个批量操作是成功执行的，所以返回 200 OK。
        // 前端将通过检查DTO内部的 IsSuccess 字段来处理每个项的状态。
        var responseDto = dictionaryResult.ToDictionaryDto();

        return new OkObjectResult(responseDto);
    }

    /// <summary>
    /// 智能地将 CollectionResult`T` 转换为 ActionResult。
    /// 逻辑与 DictionaryResult 版本完全相同，只是处理的是列表。
    /// </summary>
    /// <typeparam name="T">集合中元素的类型。</typeparam>
    /// <param name="collectionResult">要转换的CollectionResult对象。</param>
    /// <returns>一个封装了操作结果的ActionResult。</returns>
    public static ActionResult<List<SingleItemResultDto<T>>> ToActionResult<T>(
        this CollectionResult<T> collectionResult)
    {
        // 1. 检查整个批量操作是否失败。
        if (collectionResult.TryGetError(out var error))
        {
            return error.ToResult().ToActionResult();
        }

        // 2. 将内部的详细结果列表转换为DTO列表。
        // 无论内部是否有单独的项失败，整个批量操作是成功执行的，所以返回 200 OK。
        // 前端将通过检查DTO内部的 IsSuccess 字段来处理每个项的状态。
        var responseDto = collectionResult.ToListDto();

        return new OkObjectResult(responseDto);
    }

    private static bool ServerErrorToActionResult(Error error, string? errorMessage, [NotNullWhen(true)] out ActionResult? actionResult)
    {
        actionResult = null;
        if (error is not NormalError serverError)
            return false;
        (int statusCode, string defaultServerErrorMessage) = MapServerErrorTypeToHttpStatusCodeAndDefaultMessage(serverError.Code);
        actionResult = new ObjectResult(errorMessage ?? defaultServerErrorMessage) { StatusCode = statusCode };
        return true;
    }

    #region Tool

    /// <summary>
    /// 将 <see cref="NormalError.ServerErrorType"/> 枚举值映射到对应的 HTTP 状态码。
    /// </summary>
    /// <param name="errorType">服务器错误类型。</param>
    /// <returns>对应的 HTTP 状态码整数值。</returns>
    private static (int StatusCode, string defaultMessage) MapServerErrorTypeToHttpStatusCodeAndDefaultMessage(ServerErrorType errorType)
    {
        return errorType switch
        {
            // 4xx Client Errors
            ServerErrorType.BadRequest => (StatusCodes.Status400BadRequest, "请求无效或参数错误。"),
            ServerErrorType.ValidationError => (StatusCodes.Status422UnprocessableEntity, "提交的数据验证失败。"),
            ServerErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "需要有效的身份认证凭证才能访问此资源。"),
            ServerErrorType.Forbidden => (StatusCodes.Status403Forbidden, "您没有足够的权限访问此资源或执行此操作。"),
            ServerErrorType.NotFound => (StatusCodes.Status404NotFound, "无法找到您请求的资源。"),
            ServerErrorType.MethodNotAllowed => (StatusCodes.Status405MethodNotAllowed, "不允许使用当前的HTTP方法访问此资源。"),
            ServerErrorType.Conflict => (StatusCodes.Status409Conflict, "您的请求与服务器的当前状态存在冲突，无法完成。"),
            ServerErrorType.ConcurrencyConflict => (StatusCodes.Status409Conflict, "数据已被他人修改，请刷新页面后重试。"),
            ServerErrorType.PayloadTooLarge => (StatusCodes.Status413PayloadTooLarge, "您发送的请求体大小超过了服务器允许的限制。"),
            ServerErrorType.UnsupportedMediaType => (StatusCodes.Status415UnsupportedMediaType, "服务器不支持您请求中使用的媒体类型。"),
            ServerErrorType.TooManyRequests => (StatusCodes.Status429TooManyRequests, "您的请求过于频繁，已触发服务器的速率限制，请稍后再试。"),
            ServerErrorType.BusinessLogicError => (StatusCodes.Status422UnprocessableEntity, "由于业务规则的限制，您的操作无法完成。"),
            ServerErrorType.OperationCancelled => (StatusCodes.Status400BadRequest, "操作已被用户或系统取消。"),

            // 5xx Server Errors
            ServerErrorType.InternalServerError => (StatusCodes.Status500InternalServerError, "服务器在处理您的请求时遇到了未预料到的内部错误。"),
            ServerErrorType.DatabaseError => (StatusCodes.Status500InternalServerError, "与数据库服务交互时发生错误。"),
            ServerErrorType.ThirdPartyServiceError => (StatusCodes.Status502BadGateway, "依赖的外部服务未能成功响应或当前不可用。"),
            ServerErrorType.ServiceUnavailable => (StatusCodes.Status503ServiceUnavailable, "服务当前暂时不可用，我们正在处理，请稍后重试。"),
            ServerErrorType.NetworkError => (StatusCodes.Status503ServiceUnavailable, "发生网络连接问题，请检查您的网络连接或稍后重试。"),
            ServerErrorType.GatewayTimeout => (StatusCodes.Status504GatewayTimeout, "作为网关或代理的服务器未能及时从上游服务器获取响应。"),

            ServerErrorType.UnknownError or _ => (StatusCodes.Status500InternalServerError, DefaultErrorMessage), // 默认或未知错误均返回500
        };
    }

    #endregion
}