using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.Config;

/// <summary>
/// 模块的配置
/// </summary>
[JsonConverter(typeof(ModuleConfigConverter))]
public abstract record AbstractModuleConfig
{
    /// <summary>
    /// 名字
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    [Display(Name = "配置名称", Description = "模块的配置名称，用于在界面上显示。")]
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
    /// 模块的类型
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public abstract string ModuleType { get; init; }

    /// <summary>
    /// 转为实例化的运行时状态
    /// </summary>
    /// <param name="workflowRuntimeService"></param>
    /// <returns></returns>
    internal abstract IWithDebugDto<IModuleProcessorDebugDto> ToModuleProcessor(WorkflowRuntimeService workflowRuntimeService);

    /// <summary>
    /// 获得模块的输入变量
    /// </summary>
    /// <returns></returns>
    internal virtual List<string> GetConsumedVariables() => [];

    /// <summary>
    /// 获得模块的输出变量
    /// </summary>
    /// <returns></returns>
    internal virtual List<string> GetProducedVariables() => [];
}

internal abstract record AbstractModuleConfig<T> : AbstractModuleConfig
    where T : IWithDebugDto<IModuleProcessorDebugDto>
{
    /// <inheritdoc />
    public override string Name { get; init; } = string.Empty;

    /// <inheritdoc />
    public override bool Enabled { get; init; } = true;

    /// <inheritdoc/>
    public override string ConfigId { get; init; } = string.Empty;

    /// <inheritdoc/>
    public override string ModuleType { get; init; } = nameof(T);

    internal override IWithDebugDto<IModuleProcessorDebugDto> ToModuleProcessor(WorkflowRuntimeService workflowRuntimeService) =>
        this.ToCurrentModule(workflowRuntimeService);

    protected abstract T ToCurrentModule(WorkflowRuntimeService workflowRuntimeService);
}