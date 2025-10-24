using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Core.Config.Stored;
using YAESandBox.Workflow.Core.DebugDto;
using YAESandBox.Workflow.Core.Runtime.Processor;
using YAESandBox.Workflow.Core.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.Core.VarSpec;

namespace YAESandBox.Workflow.Core.Config.RuneConfig;

/// <summary>
/// 符文的配置
/// </summary>
[JsonConverter(typeof(RuneConfigConverter))]
public abstract partial record AbstractRuneConfig : IConfigStored
{
    /// <inheritdoc />
    [Required(AllowEmptyStrings = true)]
    [HiddenInForm(true)]
    [Display(Name = "配置名称", Description = "符文的配置名称，用于在界面上显示。")]
    public virtual string Name { get; init; } = string.Empty;

    /// <summary>
    /// 是否被启用，默认为True
    /// </summary>
    [Required]
    [DefaultValue(true)]
    [HiddenInForm(true)]
    public virtual bool Enabled { get; init; } = true;

    /// <summary>
    /// 唯一的 ID，在拷贝时也需要更新
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public virtual string ConfigId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 符文的类型
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public abstract string RuneType { get; init; }
}

public abstract partial record AbstractRuneConfig
{
    /// <summary>
    /// 转为实例化的运行时状态
    /// </summary>
    /// <param name="creatingContext"></param>
    /// <returns></returns>
    public abstract IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto> ToRuneProcessor(ICreatingContext creatingContext);

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
public abstract record AbstractRuneConfig<TProcessor> : AbstractRuneConfig
    where TProcessor : IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto>
{
    /// <inheritdoc />
    protected AbstractRuneConfig()
    {
        this.RuneType = this.GetType().Name;
    }

    /// <inheritdoc/>
    public sealed override string RuneType { get; init; }

    /// <inheritdoc />
    public override IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto> ToRuneProcessor(ICreatingContext creatingContext) =>
        this.ToCurrentRune(creatingContext);

    /// <summary>
    /// 用于将当前配置转为运行时对象，提供了类型提示，而非通用的接口
    /// </summary>
    /// <param name="creatingContext"></param>
    /// <returns></returns>
    protected abstract TProcessor ToCurrentRune(ICreatingContext creatingContext);
}