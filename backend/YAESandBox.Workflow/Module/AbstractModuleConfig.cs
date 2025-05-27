using YAESandBox.Workflow.DebugDto;

namespace YAESandBox.Workflow.Module;

internal abstract record AbstractModuleConfig<T> : IModuleConfig
    where T : IWithDebugDto<IModuleProcessorDebugDto>
{
    /// <inheritdoc />
    public string InstanceId { get; init; } = string.Empty;

    /// <inheritdoc />
    public string ModuleType { get; init; } = nameof(T);

    public IWithDebugDto<IModuleProcessorDebugDto> ToModuleProcessor() =>
        this.ToCurrentModule();

    protected abstract T ToCurrentModule();
}