using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.Config;

/// <summary>
/// 符文的配置
/// </summary>
[JsonConverter(typeof(RuneConfigConverter))]
public abstract record AbstractRuneConfig
{
    /// <summary>
    /// 名字
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    [Display(Name = "配置名称", Description = "符文的配置名称，用于在界面上显示。")]
    public abstract string Name { get; init; }

    /// <summary>
    /// 是否被启用，默认为True
    /// </summary>
    [Required]
    [DefaultValue(true)]
    [HiddenInForm(true)]
    public abstract bool Enabled { get; init; }

    /// <summary>
    /// 唯一的 ID，在拷贝时也需要更新
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public abstract string ConfigId { get; init; }

    /// <summary>
    /// 符文的类型
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public abstract string RuneType { get; init; }

    /// <summary>
    /// 转为实例化的运行时状态
    /// </summary>
    /// <param name="workflowRuntimeService"></param>
    /// <returns></returns>
    internal abstract IWithDebugDto<IRuneProcessorDebugDto> ToRuneProcessor(WorkflowRuntimeService workflowRuntimeService);

    /// <summary>
    /// 获得符文的输入变量
    /// </summary>
    /// <returns></returns>
    public virtual List<string> GetConsumedVariables() => [];

    /// <summary>
    /// 获得符文的输出变量
    /// </summary>
    /// <returns></returns>
    public virtual List<string> GetProducedVariables() => [];
}

/// <inheritdoc />
public abstract record AbstractRuneConfig<T> : AbstractRuneConfig
    where T : IWithDebugDto<IRuneProcessorDebugDto>
{
    /// <inheritdoc />
    public override string Name { get; init; } = string.Empty;

    /// <inheritdoc />
    public override bool Enabled { get; init; } = true;

    /// <inheritdoc/>
    public override string ConfigId { get; init; } = string.Empty;

    /// <inheritdoc/>
    public override string RuneType { get; init; } = nameof(T);

    internal override IWithDebugDto<IRuneProcessorDebugDto> ToRuneProcessor(WorkflowRuntimeService workflowRuntimeService) =>
        this.ToCurrentRune(workflowRuntimeService);

    /// <summary>
    /// 用于将当前配置转为运行时对象，提供了类型提示，而非通用的接口
    /// </summary>
    /// <param name="workflowRuntimeService"></param>
    /// <returns></returns>
    protected abstract T ToCurrentRune(WorkflowRuntimeService workflowRuntimeService);
}