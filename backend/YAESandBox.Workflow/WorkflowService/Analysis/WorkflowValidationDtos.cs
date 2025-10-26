using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using YAESandBox.Workflow.Core.Config;
using YAESandBox.Workflow.Core.Graph;

namespace YAESandBox.Workflow.WorkflowService.Analysis;


/// <summary>
/// 一条具体的校验信息。
/// </summary>
public record ValidationMessage
{
    /// <summary>
    /// 问题的严重性等级。
    /// </summary>
    [Required]
    public RuleSeverity Severity { get; init; }

    /// <summary>
    /// 具体的错误或警告文本。
    /// </summary>
    [Required]
    public required string Message { get; init; }

    /// <summary>
    /// 触发此消息的规则来源，便于前端分类处理。
    /// 例如："DataFlow", "SingleInTuum", "FormValidation"。
    /// </summary>
    [Required]
    public required string RuleSource { get; init; }
}

internal static class ConnectionIdentifier
{
    /// <summary>
    /// 为一条连接生成一个唯一的、可预测的字符串标识符。
    /// </summary>
    /// <param name="connection">工作流连接对象。</param>
    /// <returns>唯一的字符串ID。</returns>
    public static string GetId(this GraphConnection<string> connection) =>
        $"{connection.Source.NodeId}:{connection.Source.PortName}->{connection.Target.NodeId}:{connection.Target.PortName}";
}

/// <summary>
/// 严重等级
/// </summary>
public enum RuleSeverity
{
    /// <summary>
    /// 提示信息，用于提供建议或优化方案
    /// </summary>
    Hint,

    /// <summary>
    /// 警告，可能的问题，但允许编译继续
    /// </summary>
    Warning,

    /// <summary>
    /// 错误，阻止代码编译的问题
    /// </summary>
    Error,

    /// <summary>
    /// 致命错误，通常表示系统无法继续运行
    /// </summary>
    Fatal
}