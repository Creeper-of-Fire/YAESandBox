using FluentResults;
using static YAESandBox.Depend.Results.NormalError;

namespace YAESandBox.Depend.Results;

/// <summary>
/// 表示一个服务器端错误。
/// </summary>
/// <param name="Message">错误的描述信息，不能为空。</param>
/// <param name="Code">错误的类型代码，对应 ServerErrorType 枚举。</param>
public partial record NormalError(string Message, ServerErrorType Code) : LazyInitError(Message)
{
    /// <summary>
    /// 创建一个表示“未知错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.UnknownError"/></returns>
    public static Result UnknownError(string message) => new NormalError(message, ServerErrorType.UnknownError);

    /// <summary>
    /// 创建一个表示“无效请求或参数错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.BadRequest"/></returns>
    public static Result BadRequest(string message) => new NormalError(message, ServerErrorType.BadRequest);

    /// <summary>
    /// 创建一个表示“未授权”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.Unauthorized"/></returns>
    public static Result Unauthorized(string message) => new NormalError(message, ServerErrorType.Unauthorized);

    /// <summary>
    /// 创建一个表示“禁止访问”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.Forbidden"/></returns>
    public static Result Forbidden(string message) => new NormalError(message, ServerErrorType.Forbidden);

    /// <summary>
    /// 创建一个表示“未找到资源”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.NotFound"/></returns>
    public static Result NotFound(string message) => new NormalError(message, ServerErrorType.NotFound);

    /// <summary>
    /// 创建一个表示“请求方法不允许”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.MethodNotAllowed"/></returns>
    public static Result MethodNotAllowed(string message) => new NormalError(message, ServerErrorType.MethodNotAllowed);

    /// <summary>
    /// 创建一个表示“请求冲突”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.Conflict"/></returns>
    public static Result Conflict(string message) => new NormalError(message, ServerErrorType.Conflict);

    /// <summary>
    /// 创建一个表示“请求实体过大”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.PayloadTooLarge"/></returns>
    public static Result PayloadTooLarge(string message) => new NormalError(message, ServerErrorType.PayloadTooLarge);

    /// <summary>
    /// 创建一个表示“不支持的媒体类型”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.UnsupportedMediaType"/></returns>
    public static Result UnsupportedMediaType(string message) => new NormalError(message, ServerErrorType.UnsupportedMediaType);

    /// <summary>
    /// 创建一个表示“请求过于频繁”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.TooManyRequests"/></returns>
    public static Result TooManyRequests(string message) => new NormalError(message, ServerErrorType.TooManyRequests);

    /// <summary>
    /// 创建一个表示“服务器内部错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.InternalServerError"/></returns>
    public static Result Internal(string message) => new NormalError(message, ServerErrorType.InternalServerError);
    
    /// <summary>
    /// 创建一个表示“服务器内部错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.InternalServerError"/></returns>
    public static Result Error(string message) => new NormalError(message, ServerErrorType.InternalServerError);

    /// <summary>
    /// 创建一个表示“服务不可用”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.ServiceUnavailable"/></returns>
    public static Result ServiceUnavailable(string message) => new NormalError(message, ServerErrorType.ServiceUnavailable);

    /// <summary>
    /// 创建一个表示“网关超时”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.GatewayTimeout"/></returns>
    public static Result GatewayTimeout(string message) => new NormalError(message, ServerErrorType.GatewayTimeout);

    /// <summary>
    /// 创建一个表示“数据库操作错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.DatabaseError"/></returns>
    public static Result DatabaseError(string message) => new NormalError(message, ServerErrorType.DatabaseError);

    /// <summary>
    /// 创建一个表示“网络连接错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.NetworkError"/></returns>
    public static Result NetworkError(string message) => new NormalError(message, ServerErrorType.NetworkError);

    /// <summary>
    /// 创建一个表示“业务逻辑错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.BusinessLogicError"/></returns>
    public static Result BusinessLogicError(string message) => new NormalError(message, ServerErrorType.BusinessLogicError);

    /// <summary>
    /// 创建一个表示“第三方服务错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.ThirdPartyServiceError"/></returns>
    public static Result ThirdPartyServiceError(string message) => new NormalError(message, ServerErrorType.ThirdPartyServiceError);

    /// <summary>
    /// 创建一个表示“数据验证失败”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.ValidationError"/></returns>
    public static Result ValidationError(string message) => new NormalError(message, ServerErrorType.ValidationError);

    /// <summary>
    /// 创建一个表示“操作被取消”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.OperationCancelled"/></returns>
    public static Result OperationCancelled(string message) => new NormalError(message, ServerErrorType.OperationCancelled);

    /// <summary>
    /// 创建一个表示“无法处理的实体”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.UnprocessableEntity"/></returns>
    public static Result UnprocessableEntity(string message) => new NormalError(message, ServerErrorType.UnprocessableEntity);

    /// <summary>
    /// 创建一个表示“乐观并发控制失败”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.ConcurrencyConflict"/></returns>
    public static Result ConcurrencyConflict(string message) => new NormalError(message, ServerErrorType.ConcurrencyConflict);
}