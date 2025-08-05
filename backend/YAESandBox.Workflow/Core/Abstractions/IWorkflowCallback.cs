using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.Core.Abstractions;

/// <summary>
/// 工作流的回调
/// </summary>
public interface IWorkflowCallback;

public static class IWorkflowCallbackExtension
{
    public static Result<T> GetWorkflowCallback<T>(this IWorkflowCallback workflowCallback)
    {
        if (workflowCallback is T t)
            return Result.Ok(t);

        return Result.Fail($"未能找到指定的类型，{workflowCallback.GetType()} 不属于 {typeof(T)} 类型。");
    }

    public static Result<T> GetWorkflowCallback<T>(this WorkflowRuntimeService workflowRuntimeService)
    {
        return GetWorkflowCallback<T>(workflowRuntimeService.Callback);
    }

    /// <summary>
    /// 获取并使用一个具体的回调接口。
    /// </summary>
    /// <typeparam name="T">要获取的回调接口类型。</typeparam>
    /// <param name="workflowRuntimeService">运行时服务。</param>
    /// <param name="callbackAction">一个接收具体回调接口并返回Result的委托。</param>
    /// <returns>一个表示操作结果的Result。</returns>
    public static Result Callback<T>(this WorkflowRuntimeService workflowRuntimeService, Action<T> callbackAction)
    {
        var result = GetWorkflowCallback<T>(workflowRuntimeService);
        if (result.TryGetValue(out var value))
            callbackAction(value);
        return result;
    }

    /// <summary>
    /// 异步地获取并使用一个具体的回调接口。
    /// </summary>
    /// <typeparam name="T">要获取的回调接口类型。</typeparam>
    /// <param name="workflowRuntimeService">运行时服务。</param>
    /// <param name="asyncCallbackAction">一个接收具体回调接口并返回Task的异步委托。</param>
    /// <returns>一个表示操作结果的Task，其结果为Result。</returns>
    public static async Task<Result> CallbackAsync<T>(this WorkflowRuntimeService workflowRuntimeService, Func<T, Task> asyncCallbackAction)
    {
        var result = GetWorkflowCallback<T>(workflowRuntimeService);
        if (result.TryGetValue(out var value))
            await asyncCallbackAction(value);
        return result;
    }
}