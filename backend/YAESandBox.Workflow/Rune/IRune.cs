using YAESandBox.Workflow.Rune;

namespace YAESandBox.Workflow.DebugDto;

/// <summary>
/// 代表一个通用的符文
/// </summary>
public interface IRuneProcessor<out TConfig, out TDebug> : IProcessorWithDebugDto<TDebug>, IRuneProcessorWithConfig<TConfig>
    where TConfig : AbstractRuneConfig where TDebug : IRuneProcessorDebugDto;

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