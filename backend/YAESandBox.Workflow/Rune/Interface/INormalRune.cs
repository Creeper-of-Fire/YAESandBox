using YAESandBox.Depend.Results;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune.Config;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Workflow.Rune.Interface;

/// <summary>
/// 普通的符文
/// </summary>
/// <typeparam name="TConfig"></typeparam>
/// <typeparam name="TDebug"></typeparam>
public interface INormalRune<out TConfig, out TDebug> : IRuneProcessor<TConfig, TDebug>
    where TConfig : AbstractRuneConfig where TDebug : IRuneProcessorDebugDto
{
    /// <summary>
    /// 启动枢机流程
    /// </summary>
    /// <param name="tuumProcessorContent">枢机执行的上下文内容。</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default);
}