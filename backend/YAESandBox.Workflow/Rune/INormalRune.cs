using YAESandBox.Depend.Results;
using static YAESandBox.Workflow.Step.StepProcessor;

namespace YAESandBox.Workflow.Rune;

/// <summary>
/// 普通的符文
/// </summary>
public interface INormalRune
{
    /// <summary>
    /// 启动步骤流程
    /// </summary>
    /// <param name="stepProcessorContent">步骤执行的上下文内容。</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal Task<Result> ExecuteAsync(StepProcessorContent stepProcessorContent, CancellationToken cancellationToken = default);
}