using YAESandBox.Depend.Results;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Workflow.Rune;

/// <summary>
/// 普通的符文
/// </summary>
public interface INormalRune
{
    /// <summary>
    /// 启动枢机流程
    /// </summary>
    /// <param name="tuumProcessorContent">枢机执行的上下文内容。</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default);
}