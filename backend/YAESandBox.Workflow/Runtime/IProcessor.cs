namespace YAESandBox.Workflow.Runtime;

/// <summary>
/// 一个含有 <see cref="ProcessorContext"/> 的接口。
/// </summary>
public interface IProcessor
{
    /// <summary>
    /// 处理器的上下文对象。
    /// </summary>
    public ProcessorContext ProcessorContext { get; }
}