using YAESandBox.Depend.Results;
using static YAESandBox.Depend.ResultsExtend.NormalError;

namespace YAESandBox.Depend.ResultsExtend;

/// <summary>
/// 表示一个服务器端错误。
/// </summary>
/// <param name="Message">错误的描述信息，不能为空。</param>
/// <param name="Code">错误的类型代码，对应 ServerErrorType 枚举。</param>
public partial record NormalError(string Message, ServerErrorType Code) : Error(Message)
{
    /// <summary>
    /// 创建一个表示“未知错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.UnknownError"/></returns>
    public static NormalError UnknownError(string message) => new(message, ServerErrorType.UnknownError);

    /// <summary>
    /// 创建一个表示“无效请求或参数错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.BadRequest"/></returns>
    public static NormalError BadRequest(string message) => new(message, ServerErrorType.BadRequest);

    /// <summary>
    /// 创建一个表示“未授权”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.Unauthorized"/></returns>
    public static NormalError Unauthorized(string message) => new(message, ServerErrorType.Unauthorized);

    /// <summary>
    /// 创建一个表示“禁止访问”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.Forbidden"/></returns>
    public static NormalError Forbidden(string message) => new(message, ServerErrorType.Forbidden);

    /// <summary>
    /// 创建一个表示“未找到资源”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.NotFound"/></returns>
    public static NormalError NotFound(string message) => new(message, ServerErrorType.NotFound);

    /// <summary>
    /// 创建一个表示“请求方法不允许”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.MethodNotAllowed"/></returns>
    public static NormalError MethodNotAllowed(string message) => new(message, ServerErrorType.MethodNotAllowed);

    /// <summary>
    /// 创建一个表示“请求冲突”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.Conflict"/></returns>
    public static NormalError Conflict(string message) => new(message, ServerErrorType.Conflict);

    /// <summary>
    /// 创建一个表示“请求实体过大”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.PayloadTooLarge"/></returns>
    public static NormalError PayloadTooLarge(string message) => new(message, ServerErrorType.PayloadTooLarge);

    /// <summary>
    /// 创建一个表示“不支持的媒体类型”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.UnsupportedMediaType"/></returns>
    public static NormalError UnsupportedMediaType(string message) => new(message, ServerErrorType.UnsupportedMediaType);

    /// <summary>
    /// 创建一个表示“请求过于频繁”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.TooManyRequests"/></returns>
    public static NormalError TooManyRequests(string message) => new(message, ServerErrorType.TooManyRequests);

    /// <summary>
    /// 创建一个表示“服务器内部错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.InternalServerError"/></returns>
    public static NormalError Internal(string message) => new(message, ServerErrorType.InternalServerError);

    /// <summary>
    /// 创建一个表示“服务器内部错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.InternalServerError"/></returns>
    public static NormalError Error(string message) => new(message, ServerErrorType.InternalServerError);

    /// <summary>
    /// 创建一个表示“服务不可用”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.ServiceUnavailable"/></returns>
    public static NormalError ServiceUnavailable(string message) => new(message, ServerErrorType.ServiceUnavailable);

    /// <summary>
    /// 创建一个表示“网关超时”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.GatewayTimeout"/></returns>
    public static NormalError GatewayTimeout(string message) => new(message, ServerErrorType.GatewayTimeout);

    /// <summary>
    /// 创建一个表示“数据库操作错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.DatabaseError"/></returns>
    public static NormalError DatabaseError(string message) => new(message, ServerErrorType.DatabaseError);

    /// <summary>
    /// 创建一个表示“网络连接错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.NetworkError"/></returns>
    public static NormalError NetworkError(string message) => new(message, ServerErrorType.NetworkError);

    /// <summary>
    /// 创建一个表示“业务逻辑错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.BusinessLogicError"/></returns>
    public static NormalError BusinessLogicError(string message) => new(message, ServerErrorType.BusinessLogicError);

    /// <summary>
    /// 创建一个表示“第三方服务错误”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.ThirdPartyServiceError"/></returns>
    public static NormalError ThirdPartyServiceError(string message) => new(message, ServerErrorType.ThirdPartyServiceError);

    /// <summary>
    /// 创建一个表示“数据验证失败”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.ValidationError"/></returns>
    public static NormalError ValidationError(string message) => new(message, ServerErrorType.ValidationError);

    /// <summary>
    /// 创建一个表示“操作被取消”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.OperationCancelled"/></returns>
    public static NormalError OperationCancelled(string message) => new(message, ServerErrorType.OperationCancelled);

    /// <summary>
    /// 创建一个表示“无法处理的实体”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.UnprocessableEntity"/></returns>
    public static NormalError UnprocessableEntity(string message) => new(message, ServerErrorType.UnprocessableEntity);

    /// <summary>
    /// 创建一个表示“乐观并发控制失败”的 NormalError 实例。
    /// </summary>
    /// <param name="message">错误描述信息，不能为空。</param>
    /// <returns>一个新的 NormalError 实例，其 Code 为 <see cref="ServerErrorType.ConcurrencyConflict"/></returns>
    public static NormalError ConcurrencyConflict(string message) => new(message, ServerErrorType.ConcurrencyConflict);
}