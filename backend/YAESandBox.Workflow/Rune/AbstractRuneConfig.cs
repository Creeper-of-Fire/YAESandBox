using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Utility;
using YAESandBox.Workflow.VarSpec;

namespace YAESandBox.Workflow.Rune;

/// <summary>
/// 符文的配置
/// </summary>
[JsonConverter(typeof(RuneConfigConverter))]
public abstract record AbstractRuneConfig
{
    /// <summary>
    /// 名字
    /// </summary>
    [Required(AllowEmptyStrings = true)]
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
    internal abstract IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto> ToRuneProcessor(
        WorkflowRuntimeService workflowRuntimeService);

    /// <summary>
    /// 获得符文的输入变量
    /// </summary>
    /// <returns></returns>
    public virtual List<ConsumedSpec> GetConsumedSpec() => [];

    /// <summary>
    /// 获得符文的输出变量
    /// </summary>
    /// <returns></returns>
    public virtual List<ProducedSpec> GetProducedSpec() => [];
}

/// <inheritdoc />
public abstract record AbstractRuneConfig<T> : AbstractRuneConfig
    where T : IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto>
{
    /// <inheritdoc />
    protected AbstractRuneConfig()
    {
        // 3. 在构造函数中，使用 this.GetType() 来获取实际的派生类类型名
        this.RuneType = this.GetType().Name;
    }

    /// <inheritdoc />
    public override string Name { get; init; } = string.Empty;

    /// <inheritdoc />
    public override bool Enabled { get; init; } = true;

    /// <inheritdoc/>
    public override string ConfigId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc/>
    public sealed override string RuneType { get; init; }

    internal override IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto> ToRuneProcessor(
        WorkflowRuntimeService workflowRuntimeService) =>
        this.ToCurrentRune(workflowRuntimeService);

    /// <summary>
    /// 用于将当前配置转为运行时对象，提供了类型提示，而非通用的接口
    /// </summary>
    /// <param name="workflowRuntimeService"></param>
    /// <returns></returns>
    protected abstract T ToCurrentRune(WorkflowRuntimeService workflowRuntimeService);
}