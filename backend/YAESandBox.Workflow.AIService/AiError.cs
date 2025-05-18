using FluentResults;
using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.AIService;

/// <inheritdoc cref="AiError.Error"/>
public record AiError(string Message) : LazyInitError(Message)
{
    /// <summary>
    /// 由于Ai服务输出的错误五花八门，所以这里只能使用最简单的字符串表示错误类型。
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public static Result Error(string message) => new AiError(message);
}