using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune.Config;
using YAESandBox.Workflow.Runtime;

namespace YAESandBox.Workflow.Rune.Interface;

/// <summary>
/// 代表一个符文的运行时状态，含有符文的配置信息，以及运行时Debug状态信息。
/// </summary>
public interface IRuneProcessor<out TConfig, out TDebug> : IProcessorWithDebugDto<TDebug>, IRuneProcessorWithConfig<TConfig>, IRuneProcessor
    where TConfig : AbstractRuneConfig where TDebug : IRuneProcessorDebugDto;

/// <summary>
/// 代表一个符文的运行时状态
/// </summary>
public interface IRuneProcessor : IProcessor;

/// <summary>
/// 一种携带了自身配置的符文类型
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IRuneProcessorWithConfig<out T> where T : AbstractRuneConfig
{
    /// <summary>
    /// 配置
    /// </summary>
    T Config { get; }
}