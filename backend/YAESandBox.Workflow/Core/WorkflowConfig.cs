using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Tuum;

namespace YAESandBox.Workflow.Core;

/// <summary>
/// 工作流的配置
/// </summary>
public record WorkflowConfig
{
    /// <summary>
    /// 名字
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 声明此工作流启动时需要提供的入口参数列表。
    /// 这些输入可以作为连接的源头。
    /// </summary>
    [Required]
    public List<string> WorkflowInputs { get; init; } = [];

    /// <summary>
    /// 一个工作流含有的枢机（有序）
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public List<TuumConfig> Tuums { get; init; } = [];

    /// <summary>
    /// 定义了工作流中所有枢机(Tuum)的图结构和连接行为。
    /// <para>如果此对象为null，系统将默认采用自动连接模式。</para>
    /// </summary>
    [HiddenInForm(true)]
    [Display(Name = "工作流图配置", Description = "配置工作流中所有枢机的连接方式，包括手动连接和是否启用自动连接等。")]
    public WorkflowGraphConfig? Graph { get; init; }

    /// <summary>
    /// 工作流的标签，属于元数据，用于筛选等。
    /// </summary>
    public List<string>? Tags { get; init; }
}

/// <summary>
/// 封装了工作流中枢机(Tuum)图的连接配置。
/// </summary>
public record WorkflowGraphConfig
{
    /// <summary>
    /// 控制是否启用基于命名约定的自动连接功能。
    /// <para>当为 true 时，系统会尝试自动连接所有枢机，忽略下面的 Connections 列表。</para>
    /// <para>当为 false 时，系统将严格使用 Connections 列表进行手动连接。</para>
    /// </summary>
    [Required]
    [DefaultValue(true)]
    [Display(Name = "启用自动连接", Description = "如果启用，将根据枢机的顺序和端口名自动连接。如果禁用，则必须手动提供所有连接。")]
    public bool EnableAutoConnect { get; init; } = true;

    /// <summary>
    /// 当 EnableAutoConnect 为 false 时，用于定义工作流中所有枢机之间的显式连接。
    /// </summary>
    [Display(Name = "手动连接列表", Description = "当禁用自动连接时，在此处定义所有枢机之间的数据流向。")]
    public List<WorkflowConnection>? Connections { get; init; } = [];
}

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