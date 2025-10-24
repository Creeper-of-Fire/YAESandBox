using YAESandBox.Workflow.Core.Runtime.Processor;

namespace YAESandBox.Workflow.Core.DebugDto;

/// <summary>
/// 一种携带了DebugDTO的类型
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IProcessorWithDebugDto<out T> : IProcessor
    where T : IDebugDto
{
    /// <summary>
    /// 获得Debug信息
    /// </summary>
    T DebugDto { get; }
}