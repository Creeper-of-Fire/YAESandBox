using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Depend.Results;

namespace YAESandBox.Depend.AspNetCore;

/// <summary>
/// 提供将 FluentResults.Result 对象转换为 ASP.NET Core ActionResult 的扩展方法。
///这些方法旨在简化控制器中处理操作结果并将其映射到适当 HTTP 响应的模式。
/// </summary>
public static partial class ResultToActionExtensions
{
    internal const string DefaultErrorMessage = "发生内部未知错误。";

    /// <summary>
    /// 为 <see cref="ControllerBase"/> 创建一个表示 HTTP 500 内部服务器错误的 <see cref="ObjectResult"/>。
    /// 此方法假定传入的 <paramref name="result"/> 必然是失败的。
    /// 它主要用于控制器中，当已经判断操作失败后，快速生成一个标准的500错误响应。
    /// </summary>
    /// <typeparam name="T">结果中值的类型（虽然在此方法中，我们只关心错误）。</typeparam>
    /// <param name="controller">当前的控制器实例。</param>
    /// <param name="result">失败的 FluentResults.Result <typeparamref name="T"/> 对象。</param>
    /// <returns>一个包含错误消息的 <see cref="ObjectResult"/>，状态码为500。</returns>
    /// <exception cref="UnreachableException">如果传入的 <paramref name="result"/> 是成功的，则抛出此异常，因为这违反了该方法的使用前提。</exception>
    public static ObjectResult Get500ErrorResult<T>(this ControllerBase controller, Result<T> result) =>
        controller.Get500ErrorResult(result.ToResult()); // 转换为非泛型 Result 并调用重载方法

    /// <summary>
    /// 为 <see cref="ControllerBase"/> 创建一个表示 HTTP 500 内部服务器错误的 <see cref="ObjectResult"/>。
    /// 此方法假定传入的 <paramref name="result"/> 必然是失败的。
    /// 它主要用于控制器中，当已经判断操作失败后，快速生成一个标准的500错误响应。
    /// </summary>
    /// <param name="controller">当前的控制器实例。</param>
    /// <param name="result">失败的 FluentResults.Result 对象。</param>
    /// <returns>一个包含错误消息的 <see cref="ObjectResult"/>，状态码为500。</returns>
    /// <exception cref="UnreachableException">如果传入的 <paramref name="result"/> 是成功的，则抛出此异常，因为这违反了该方法的使用前提。</exception>
    public static ObjectResult Get500ErrorResult(this ControllerBase controller, Result result)
    {
        if (result.TryGetError(out var error))
            // 返回 500 Internal Server Error 和错误消息
            return controller.StatusCode(StatusCodes.Status500InternalServerError, error);
        // 防御性编程：检查是否错误地传入了成功的结果
        throw new UnreachableException($"GetErrorResult: {result} 必定是失败的，但却输入了成功的情况。"); // 如果成功，则表示逻辑错误
    }
}

/// <summary>
/// 定义错误数据传输对象（DTO）的接口。
/// 确保所有错误 DTO 实现都具有 Message 属性。
/// </summary>
public interface IErrorDto
{
    /// <summary>
    /// 获取或设置错误消息。
    /// </summary>
    string Message { get; set; }
}

/// <summary>
/// 表示一个标准的错误数据传输对象（DTO）。
/// 用于在 API 响应中序列化错误信息。
/// </summary>
public class ErrorDto : IErrorDto
{
    /// <summary>
    /// 获取或设置错误消息。
    /// </summary>
    public string Message { get; set; } = string.Empty; // 初始化以避免 null
}