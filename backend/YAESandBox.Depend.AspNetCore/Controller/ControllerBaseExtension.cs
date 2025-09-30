using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Depend.Results;

namespace YAESandBox.Depend.AspNetCore.Controller;

/// <summary>
/// 为 ControllerBase 提供用于创建标准 API 响应的扩展方法。
/// </summary>
public static class ControllerBaseExtension
{
    /// <summary>
    /// 创建一个表示内部服务器错误 (500) 的 ObjectResult，使用标准的 Error.ToDetailString() 格式化响应体。
    /// </summary>
    /// <param name="controller">ControllerBase 实例。</param>
    /// <param name="error">包含所有错误信息的 Error 对象。</param>
    /// <returns>一个配置为 500 状态码的 ObjectResult。</returns>
    public static ObjectResult InternalServerError(this ControllerBase controller, Error error)
    {
        // 统一使用 Error.ToDetailString() 来生成响应体
        string responseBody = error.ToDetailString();
        return controller.StatusCode(StatusCodes.Status500InternalServerError, responseBody);
    }
    
    /// <summary>
    /// 创建一个表示内部服务器错误 (500) 的 ObjectResult，并将异常信息格式化为响应体。
    /// </summary>
    /// <param name="controller">ControllerBase 实例。</param>
    /// <param name="ex">导致错误的异常对象。</param>
    /// <param name="customErrorMessage">可选的自定义消息，如果提供，将显示在通用消息之前。</param>
    /// <returns>一个配置为 500 状态码的 ObjectResult。</returns>
    public static ObjectResult InternalServerError(this ControllerBase controller, Exception ex, string? customErrorMessage = null)
    {
        // 1. 将 Exception 和自定义消息封装成一个标准的 Error 对象
        var error = new Error(customErrorMessage ?? ex.Message, ex);

        // 2. 调用核心方法
        return controller.InternalServerError(error);
    }

    /// <summary>
    /// 创建一个表示内部服务器错误 (500) 的 ObjectResult，使用简单的字符串消息。
    /// </summary>
    /// <param name="controller">ControllerBase 实例。</param>
    /// <param name="errorMessage">要包含在响应体中的错误消息。</param>
    /// <returns>一个配置为 500 状态码的 ObjectResult。</returns>
    public static ObjectResult InternalServerError(this ControllerBase controller, string errorMessage)
    {
        // 1. 将字符串消息封装成一个标准的 Error 对象
        var error = new Error(errorMessage);

        // 2. 调用核心方法
        return controller.InternalServerError(error);
    }
}