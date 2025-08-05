namespace YAESandBox.Workflow.Core.Abstractions;

/// <summary>
/// 一个暂时的回调
/// </summary>
/// <param name="requestDisplayUpdateCallback"></param>
/// <param name="sendFinalRawText"></param>
public class WorkflowRawTextCallbackTemp(
    Func<DisplayUpdateRequestPayload, Task> requestDisplayUpdateCallback,
    Func<string, Task> sendFinalRawText)
    : IWorkflowCallback, IWorkflowCallbackDisplayUpdate, IWorkflowCallbackSendFinalRawText
{
    /// <inheritdoc />
    public async Task DisplayUpdateAsync(string content, UpdateMode updateMode = UpdateMode.FullSnapshot) =>
        await requestDisplayUpdateCallback(new DisplayUpdateRequestPayload(content, updateMode));

    /// <inheritdoc />
    public async Task SendFinalRawTextAsync(string finalRawText) => await sendFinalRawText(finalRawText);
}

/// <summary>
/// 拥有输出显示的回调
/// </summary>
public interface IWorkflowCallbackDisplayUpdate
{
    /// <summary>
    /// 输出显示的回调
    /// </summary>
    Task DisplayUpdateAsync(string content, UpdateMode updateMode = UpdateMode.FullSnapshot);
}

/// <summary>
/// 可以最终输出RawText的回调
/// </summary>
public interface IWorkflowCallbackSendFinalRawText
{
    /// <summary>
    /// 最终输出RawText的回调
    /// </summary>
    Task SendFinalRawTextAsync(string finalRawText);
}