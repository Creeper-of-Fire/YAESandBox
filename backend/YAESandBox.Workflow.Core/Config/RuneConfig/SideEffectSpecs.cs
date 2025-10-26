using System.ComponentModel.DataAnnotations;
using YAESandBox.Workflow.Core.Runtime.WorkflowService.Abstractions;
using YAESandBox.Workflow.Core.VarSpec;

namespace YAESandBox.Workflow.Core.Config.RuneConfig;

/// <summary>
/// 描述一个由工作流向外部发射的事件的静态契约。
/// 这是工作流“API文档”的一部分，用于描述其副作用。
/// </summary>
public record EmittedEventSpec
{
    /// <summary>
    /// 事件发射到的逻辑地址（Path）。
    /// </summary>
    [Required]
    public required string Address { get; init; }

    /// <summary>
    /// 事件的更新模式（全量快照或增量）。
    /// </summary>
    [Required]
    public required UpdateMode Mode { get; init; }

    /// <summary>
    /// 对该事件用途的人类可读描述。</summary>
    [Required]
    public required string Description { get; init; }

    /// <summary>
    /// 声明此事件的源符文的ConfigId。
    /// </summary>
    [Required]
    public required string SourceRuneConfigId { get; init; }

    /// <summary>
    /// 对事件内容的详细描述，可为空。
    /// </summary>
    public EmittedContentSpec? ContentSpec { get; init; }
}

/// <summary>
/// 封装了对事件“内容”的详细规格说明。
/// 这是一个可扩展的结构，未来可以添加更多描述方式（如Schema引用、示例值等）。
/// </summary>
public record EmittedContentSpec
{
    /// <summary>
    /// 事件所携带数据的静态类型定义。对于类型不确定的情况，可为null或Any类型。
    /// </summary>
    public VarSpecDef? TypeDefinition { get; init; }

    // 未来可以在这里添加：
    // object? ExampleValue,
    // string? JsonSchemaUrl,
    // string? Version
}