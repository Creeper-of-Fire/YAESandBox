using System.ComponentModel.DataAnnotations;

namespace YAESandBox.Workflow.Core;

/// <summary>
/// 代表工作流中一个可连接的端点。
/// 它由枢机的唯一ID和该枢机上的一个输入/输出变量名组成。
/// </summary>
public record TuumConnectionEndpoint
{
    /// <summary>端点所属枢机的ConfigId。</summary>
    [Required]
    public string TuumId { get; init; } = string.Empty;

    /// <summary>端点的名称，对应于TuumConfig中Input/Output Mappings的Value。</summary>
    [Required]
    public string EndpointName { get; init; } = string.Empty;
}

/// <summary>
/// 定义了工作流中两个枢机端点之间的一条有向连接。
/// </summary>
public record WorkflowConnection
{
    /// <summary>数据来源的输出端点。</summary>
    [Required]
    public TuumConnectionEndpoint Source { get; init; } = new();

    /// <summary>数据流向的输入端点。</summary>
    [Required]
    public TuumConnectionEndpoint Target { get; init; } = new();
}