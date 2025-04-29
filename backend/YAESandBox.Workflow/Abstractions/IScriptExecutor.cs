// --- File: YAESandBox.Workflow/Abstractions/IScriptExecutor.cs ---
using System.Threading.Tasks;

namespace YAESandBox.Workflow.Abstractions;

/// <summary>
/// 负责执行 C# 脚本。
/// </summary>
public interface IScriptExecutor
{
    /// <summary>
    /// 执行给定的 C# 脚本内容。
    /// </summary>
    /// <param name="scriptContent">要执行的脚本代码。</param>
    /// <param name="executionContext">提供给脚本的执行上下文。</param>
    /// <param name="description">(可选) 脚本描述，用于日志或错误信息。</param>
    /// <returns>表示异步操作的任务。</returns>
    /// <exception cref="ScriptExecutionException">当脚本执行出错时抛出。</exception>
    Task ExecuteAsync(string scriptContent, IScriptExecutionContext executionContext, string? description = null);
}