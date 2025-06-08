using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.Config;

/// <summary>
/// 模组的配置
/// </summary>
[JsonConverter(typeof(ModuleConfigConverter))]
public abstract record AbstractModuleConfig
{
    /// <summary>
    /// 唯一的 ID，在拷贝时也需要更新
    /// </summary>
    [Required]
    [HiddenInSchema(true)]
    public abstract string ConfigId { get; init; }

    /// <summary>
    /// 模块的类型
    /// </summary>
    [Required]
    [HiddenInSchema(true)]
    public abstract string ModuleType { get; init; }

    /// <summary>
    /// 输入变量名
    /// </summary>
    [Required]
    [HiddenInSchema(true)]
    public abstract List<string> Consumes { get; init; }

    /// <summary>
    /// 输出变量名
    /// </summary>
    [Required]
    [HiddenInSchema(true)]
    public abstract List<string> Produces { get; init; }

    internal abstract IWithDebugDto<IModuleProcessorDebugDto> ToModuleProcessor(WorkflowRuntimeService workflowRuntimeService);
}

internal abstract record AbstractModuleConfig<T> : AbstractModuleConfig
    where T : IWithDebugDto<IModuleProcessorDebugDto>
{
    /// <inheritdoc cref="AbstractModuleConfig.ConfigId"/>
    public override string ConfigId { get; init; } = string.Empty;

    /// <inheritdoc cref="AbstractModuleConfig.ConfigId"/>
    public override string ModuleType { get; init; } = nameof(T);

    /// <inheritdoc />
    public override List<string> Consumes { get; init; } = [];

    /// <inheritdoc />
    public override List<string> Produces { get; init; } = [];

    internal override IWithDebugDto<IModuleProcessorDebugDto> ToModuleProcessor(WorkflowRuntimeService workflowRuntimeService) =>
        this.ToCurrentModule(workflowRuntimeService);

    protected abstract T ToCurrentModule(WorkflowRuntimeService workflowRuntimeService);
}