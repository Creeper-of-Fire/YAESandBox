using System.ComponentModel.DataAnnotations;

namespace YAESandBox.Workflow.Analysis;

/// <summary>
/// 整个工作流的校验报告。
/// </summary>
public record WorkflowValidationReport
{
    /// <summary>
    /// 每个步骤的校验结果。
    /// Key是步骤的InstanceId。
    /// </summary>
    [Required]
    public Dictionary<string, StepValidationResult> StepResults { get; init; } = [];
}

/// <summary>
/// 单个步骤的校验结果。
/// </summary>
public record StepValidationResult
{
    /// <summary>
    /// 该步骤内每个模块的校验结果。
    /// Key是模块的ConfigId。
    /// </summary>
    [Required]
    public Dictionary<string, ModuleValidationResult> ModuleResults { get; init; } = [];

    /// <summary>
    /// 仅针对步骤本身的校验信息。
    /// </summary>
    [Required]
    public List<ValidationMessage> StepMessages { get; init; } = [];
}

/// <summary>
/// 单个模块的校验结果。
/// </summary>
public record ModuleValidationResult
{
    /// <summary>
    /// 针对该模块的校验信息列表。
    /// </summary>
    [Required]
    public List<ValidationMessage> ModuleMessages { get; init; } = [];
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
    /// 例如："DataFlow", "SingleInStep", "FormValidation"。
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