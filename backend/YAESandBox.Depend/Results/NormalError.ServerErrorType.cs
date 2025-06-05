namespace YAESandBox.Depend.Results;

public partial record NormalError
{
    /// <summary>
    /// 定义了与服务器通信时可能遇到的常见错误类型。
    /// 这些枚举成员旨在与服务器端返回的错误代码或类型进行对应，
    /// 以便前端能够根据不同的错误类型做出相应的处理或展示。
    /// </summary>
    public enum ServerErrorType
    {
        /// <summary>
        /// 未知错误。
        /// 通常表示发生了意外情况，服务器未能识别或归类该错误。
        /// </summary>
        UnknownError,

        /// <summary>
        /// 无效请求或参数错误。
        /// 通常表示客户端发送的请求数据不符合服务器的预期格式、缺少必要参数或参数值无效。
        /// 对应 HTTP 状态码 400 (Bad Request) 的场景。
        /// </summary>
        BadRequest,

        /// <summary>
        /// 未授权。
        /// 表示请求需要用户认证，但客户端未提供有效的认证信息（如Token缺失或无效）。
        /// 对应 HTTP 状态码 401 (Unauthorized) 的场景。
        /// </summary>
        Unauthorized,

        /// <summary>
        /// 禁止访问。
        /// 表示客户端已通过认证，但没有足够的权限访问请求的资源。
        /// 对应 HTTP 状态码 403 (Forbidden) 的场景。
        /// </summary>
        Forbidden,

        /// <summary>
        /// 未找到资源。
        /// 表示服务器无法根据客户端的请求找到对应的资源（如URL错误、请求的实体不存在）。
        /// 对应 HTTP 状态码 404 (Not Found) 的场景。
        /// </summary>
        NotFound,

        /// <summary>
        /// 请求方法不允许。
        /// 表示客户端使用了服务器不支持的HTTP请求方法访问特定资源 (例如对只读资源使用POST)。
        /// 对应 HTTP 状态码 405 (Method Not Allowed) 的场景。
        /// </summary>
        MethodNotAllowed,

        /// <summary>
        /// 请求冲突。
        /// 表示请求与服务器当前状态冲突，例如尝试创建一个已存在的唯一资源，或编辑一个已被他人修改的资源（版本冲突）。
        /// 对应 HTTP 状态码 409 (Conflict) 的场景。
        /// </summary>
        Conflict,

        /// <summary>
        /// 请求实体过大。
        /// 表示客户端发送的请求体大小超过了服务器的处理能力上限。
        /// 对应 HTTP 状态码 413 (Payload Too Large) 的场景。
        /// </summary>
        PayloadTooLarge,

        /// <summary>
        /// 不支持的媒体类型。
        /// 表示服务器无法处理请求中附带的媒体类型 (例如，API期望JSON，但收到了XML)。
        /// 对应 HTTP 状态码 415 (Unsupported Media Type) 的场景。
        /// </summary>
        UnsupportedMediaType,

        /// <summary>
        /// 请求过于频繁。
        /// 表示客户端在短时间内发送了过多的请求，触发了服务器的速率限制策略。
        /// 对应 HTTP 状态码 429 (Too Many Requests) 的场景。
        /// </summary>
        TooManyRequests,

        /// <summary>
        /// 服务器内部错误。
        /// 表示服务器在处理请求时遇到了未预料到的内部问题，导致无法完成请求。
        /// 对应 HTTP 状态码 500 (Internal Server Error) 的场景。
        /// </summary>
        InternalServerError,

        /// <summary>
        /// 服务不可用。
        /// 表示服务器当前无法处理请求，通常是由于临时过载或正在进行维护。
        /// 对应 HTTP 状态码 503 (Service Unavailable) 的场景。
        /// </summary>
        ServiceUnavailable,

        /// <summary>
        /// 网关超时。
        /// 表示作为网关或代理的服务器未及时从上游服务器接收到响应。
        /// 对应 HTTP 状态码 504 (Gateway Timeout) 的场景。
        /// </summary>
        GatewayTimeout,

        /// <summary>
        /// 数据库操作错误。
        /// 特指在进行数据库交互时发生的错误，可能是更具体的内部错误。
        /// </summary>
        DatabaseError,

        /// <summary>
        /// 网络连接错误。
        /// 表示客户端与服务器之间或服务器与其他服务之间的网络连接出现问题。
        /// </summary>
        NetworkError,

        /// <summary>
        /// 业务逻辑错误。
        /// 表示请求本身格式正确且用户已授权，但由于不满足某些业务规则而无法完成操作。
        /// 例如：库存不足、用户余额不足等。
        /// </summary>
        BusinessLogicError,

        /// <summary>
        /// 第三方服务错误。
        /// 表示服务器依赖的外部第三方服务返回错误或不可用。
        /// </summary>
        ThirdPartyServiceError,

        /// <summary>
        /// 数据验证失败。
        /// 比 BadRequest 更具体，指服务器端对提交的数据模型进行验证时发现不符合规则。
        /// 通常在API控制器或服务层进行模型验证后抛出。
        /// </summary>
        ValidationError, // 与 BadRequest 类似，但有时需要更细分的场景

        /// <summary>
        /// 操作被取消。
        /// 表示一个长时间运行的操作被用户或系统主动取消。
        /// </summary>
        OperationCancelled,

        /// <summary>
        /// 无法处理的实体。
        /// 表示服务器理解请求的语法，但是请求中包含的内容无法被服务器理解、不符合业务逻辑，或者请求格式错误。
        /// </summary>
        UnprocessableEntity,

        /// <summary>
        /// 乐观并发控制失败。
        /// 当更新一个资源时，该资源在读取后到提交更新前已被其他操作修改，导致版本不一致。
        /// </summary>
        ConcurrencyConflict, // 与 Conflict 类似，但特指并发场景
    }
}