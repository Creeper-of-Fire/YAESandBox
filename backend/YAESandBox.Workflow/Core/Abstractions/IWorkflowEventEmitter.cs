using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.Core.Abstractions;

/// <summary>
/// 定义了工作流向外部世界发射事件的统一接口。
/// </summary>
public interface IWorkflowEventEmitter
{
    /// <summary>
    /// 向指定的逻辑地址发射一个数据载荷。
    /// </summary>
    /// <param name="payload">要发射的事件载荷。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>一个表示异步操作结果的Result。</returns>
    Task<Result> EmitAsync(EmitPayload payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// 描述一次发射事件的完整载荷。
/// </summary>
/// <param name="Address">
/// **可空，为空时代表发送到某个设定好的根。**
/// 目标逻辑地址，使用点号分隔。
/// 例如："ui.chat_window", "data.final_summary", "debug.rune_xyz.status"
/// </param>
/// <param name="Data">要发送的数据，可以是任何可序列化的对象。</param>
/// <param name="Mode">更新模式：是全量替换还是增量附加。</param>
public record EmitPayload(
    string Address,
    object? Data,
    UpdateMode Mode = UpdateMode.FullSnapshot
);

/// <summary>
/// 指示消息的更新方式。
/// </summary>
public enum UpdateMode
{
    /// <summary>
    /// 消息包含目标区域的完整内容，应替换现有内容。
    /// </summary>
    FullSnapshot,

    /// <summary>
    /// 消息包含对现有内容的增量更改。
    /// </summary>
    Incremental
}