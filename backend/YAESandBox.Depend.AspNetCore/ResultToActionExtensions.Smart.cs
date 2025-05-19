using System.Diagnostics.CodeAnalysis;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Depend.Results;
using static YAESandBox.Depend.Results.NormalError;

namespace YAESandBox.Depend.AspNetCore;

public static partial class ResultToActionExtensions
{
    /// <summary>
    /// 智能地将 FluentResults.Result<typeparamref name="T"/> 转换为 ActionResult<typeparamref name="T"/>。
    /// 成功时，返回包含值的 OkObjectResult (HTTP 200 OK)。
    /// 失败时，检查第一个错误：
    /// - 如果错误是 <see cref="NormalError"/> 类型并包含有效的 <see cref="NormalError.ServerErrorType"/> Code，
    ///   则根据该 Code 映射到相应的 HTTP 状态码，并返回包含错误消息的 ObjectResult。
    /// - 否则，返回包含第一个错误消息（或默认消息）的 ObjectResult，状态码为 500 Internal Server Error。
    /// </summary>
    /// <typeparam name="T">结果中值的类型。</typeparam>
    /// <param name="result">要转换的 FluentResults.Result<typeparamref name="T"/> 对象。</param>
    /// <param name="defaultMessage">如果结果失败且没有错误消息，则使用的默认错误消息。</param>
    /// <returns>
    /// 成功时为 <see cref="OkObjectResult"/>；
    /// 失败时为 <see cref="ObjectResult"/>，其状态码根据错误类型智能决定。
    /// </returns>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result, string? defaultMessage = null)
    {
        if (result.TryGetValue(out var value)) // 尝试获取成功的结果值
            return new OkObjectResult(value); // 成功，返回 200 OK 和值

        // 处理失败情况
        var firstError = result.Errors.FirstOrDefault();
        string? errorMessage = firstError?.Message ?? defaultMessage;

        if (ServerErrorToActionResult(result.ToResult(), errorMessage, out var actionResult))
            return actionResult;

        // 传统流程/默认行为：如果不是 NormalError 或没有特定 Code，返回 500
        return new ObjectResult(errorMessage ?? DefaultErrorMessage) { StatusCode = StatusCodes.Status500InternalServerError };
    }

    /// <summary>
    /// 智能地将 FluentResults.Result (非泛型) 转换为 ActionResult。
    /// 成功时，返回 NoContentResult (HTTP 204 No Content)。
    /// 失败时，检查第一个错误：
    /// - 如果错误是 <see cref="NormalError"/> 类型并包含有效的 <see cref="NormalError.ServerErrorType"/> Code，
    ///   则根据该 Code 映射到相应的 HTTP 状态码，并返回包含错误消息的 ObjectResult。
    /// - 否则，返回包含第一个错误消息（或默认消息）的 ObjectResult，状态码为 500 Internal Server Error。
    /// </summary>
    /// <param name="result">要转换的 FluentResults.Result 对象。</param>
    /// <param name="defaultMessage">如果结果失败且没有错误消息，则使用的默认错误消息。</param>
    /// <returns>
    /// 成功时为 <see cref="NoContentResult"/>；
    /// 失败时为 <see cref="ObjectResult"/>，其状态码根据错误类型智能决定。
    /// </returns>
    public static ActionResult ToActionResult(this Result result, string? defaultMessage = null)
    {
        if (result.IsSuccess) // 检查结果是否成功
            return new NoContentResult(); // 成功，返回 204 No Content

        // 处理失败情况
        var firstError = result.Errors.FirstOrDefault();
        string? errorMessage = firstError?.Message ?? defaultMessage;

        if (ServerErrorToActionResult(result, errorMessage, out var actionResult))
            return actionResult;

        // 传统流程/默认行为：如果不是 NormalError 或没有特定 Code，返回 500
        return new ObjectResult(errorMessage ?? DefaultErrorMessage) { StatusCode = StatusCodes.Status500InternalServerError };
    }

    private static bool ServerErrorToActionResult(Result result, string? errorMessage, [NotNullWhen(true)] out ActionResult? actionResult)
    {
        actionResult = null;
        if (!result.HasError<NormalError>(out var serverErrors))
            return false;
        var serverError = serverErrors.FirstOrDefault();
        if (serverError is null)
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