namespace YAESandBox.Depend;

/// <summary>
/// 表示 Block 的不同状态。
/// </summary>
public enum BlockStatusCode
{
    /// <summary>
    /// Block 正在由一等公民工作流处理（例如 AI 生成内容、执行指令）。
    /// 针对此 Block 的修改将被暂存。
    /// </summary>
    Loading,

    /// <summary>
    /// Block 已生成，处于空闲状态，可以接受修改或作为新 Block 的父级。
    /// </summary>
    Idle,

    /// <summary>
    /// 工作流执行完毕，但检测到与暂存的用户指令存在冲突，等待解决。
    /// </summary>
    ResolvingConflict,

    /// <summary>
    /// Block 处理过程中发生错误。
    /// </summary>
    Error,

    /// <summary>
    /// Block 不存在。某些地方强制要求返回一个返回值，但是没找到block又不想返回null时使用
    /// </summary>
    NotFound
}

/// <summary>
/// 表示管理操作的通用结果枚举。
/// 包含来自 DeleteResult 和 MoveResult 的可能状态，以及创建操作的状态。
/// </summary>
public enum ManagementResult
{
    /// <summary>
    /// 成功
    /// </summary>
    Success,

    /// <summary>
    /// 目标或者父 Block 不存在
    /// </summary>
    NotFound,

    /// <summary>
    /// 根 Block 无法被操作
    /// </summary>
    CannotPerformOnRoot,

    /// <summary>
    /// 状态不允许操作
    /// </summary>
    InvalidState,

    /// <summary>
    /// 循环操作，如移动 Block 为其子 Block
    /// </summary>
    CyclicOperation,

    /// <summary>
    /// 请求错误
    /// </summary>
    BadRequest,

    /// <summary>
    /// 内部错误
    /// </summary>
    Error
}

public enum BlockResultCode
{
    /// <summary>
    /// 成功
    /// </summary>
    Success,

    /// <summary>
    /// 目标Block未找到
    /// </summary>
    NotFound,

    /// <summary>
    /// 输入无效
    /// </summary>
    InvalidInput,

    /// <summary>
    /// Block状态无效（如在不允许修改时修改）
    /// </summary>
    InvalidState,

    /// <summary>
    /// 未授权
    /// </summary>
    Unauthorized,

    /// <summary>
    /// 禁止
    /// </summary>
    Forbidden,

    /// <summary>
    /// 循环操作
    /// </summary>
    CyclicOperation,

    /// <summary>
    /// 冲突
    /// </summary>
    Conflict,

    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// 不支持
    /// </summary>
    NotSupported,

    /// <summary>
    /// 超时
    /// </summary>
    Timeout,

    /// <summary>
    /// 未定义，可能出错可能没有出错，可能成功可能没有成功，可能既没有成功也没有失败
    /// </summary>
    Undefined
}