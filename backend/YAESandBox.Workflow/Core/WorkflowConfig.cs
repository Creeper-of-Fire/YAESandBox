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
    /// 一个工作流含有的祝祷（有序）
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public List<TuumConfig> Tuums { get; init; } = [];
    
    /// <summary>
    /// 定义了工作流中所有祝祷之间的显式连接。
    /// 这是工作流数据流向的唯一依据。
    /// </summary>
    [Required]
    public List<WorkflowConnection> Connections { get; init; } = [];
}