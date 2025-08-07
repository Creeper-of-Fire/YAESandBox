using System.ComponentModel.DataAnnotations;

namespace YAESandBox.Workflow.Core.Analysis;

/// <summary>
/// 整个工作流的校验报告。
/// </summary>
public record WorkflowValidationReport
{
    /// <summary>
    /// 每个枢机的校验结果。
    /// Key是枢机的ConfigId。
    /// </summary>
    [Required]
    public Dictionary<string, TuumValidationResult> TuumResults { get; init; } = [];
}

/// <summary>
/// 单个枢机的校验结果。
/// </summary>
public record TuumValidationResult
{
    /// <summary>
    /// 该枢机内每个符文的校验结果。
    /// Key是符文的ConfigId。
    /// </summary>
    [Required]
    public Dictionary<string, RuneValidationResult> RuneResults { get; init; } = [];

    /// <summary>
    /// 仅针对枢机本身的校验信息。
    /// </summary>
    [Required]
    public List<ValidationMessage> TuumMessages { get; init; } = [];
}

/// <summary>
/// 单个符文的校验结果。
/// </summary>
public record RuneValidationResult
{
    /// <summary>
    /// 针对该符文的校验信息列表。
    /// </summary>
    [Required]
    public List<ValidationMessage> RuneMessages { get; init; } = [];
}

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