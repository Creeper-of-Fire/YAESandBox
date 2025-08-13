using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.Core.Abstractions;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Tuum;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Rune.ExactRune.EmitEventRuneProcessor;

namespace YAESandBox.Workflow.Rune.ExactRune;

/// <summary>
/// 发射事件符文的运行时
/// </summary>
/// <param name="config"></param>
/// <param name="workflowRuntimeService"></param>
internal class EmitEventRuneProcessor(EmitEventRuneConfig config, WorkflowRuntimeService workflowRuntimeService)
    : INormalRune<EmitEventRuneConfig, EmitEventRuneProcessorDebugDto>
{
    /// <inheritdoc />
    public EmitEventRuneConfig Config { get; } = config;

    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

    /// <inheritdoc />
    public EmitEventRuneProcessorDebugDto DebugDto { get; } = new();

    /// <inheritdoc />
    public record EmitEventRuneProcessorDebugDto : IRuneProcessorDebugDto
    {
        /// <summary>
        /// 
        /// </summary>
        public string? SourceVariableName { get; set; }

        public string? TargetAddress { get; set; }
        public UpdateMode? UpdateMode { get; set; }
        public bool EventEmitted { get; set; }
        public string? EmittedDataPreview { get; set; } // 预览发射的数据
    }

    public async Task<Result> ExecuteAsync(TuumProcessor.TuumProcessorContent tuumProcessorContent,
        CancellationToken cancellationToken = default)
    {
        // 填充调试信息
        this.DebugDto.SourceVariableName = this.Config.SourceVariableName;
        this.DebugDto.TargetAddress = this.Config.TargetAddress;
        this.DebugDto.UpdateMode = this.Config.UpdateMode;

        // 从枢机变量池中获取源数据
        object? sourceData = tuumProcessorContent.GetTuumVar(this.Config.SourceVariableName);

        // 如果源数据为null，我们可以选择不发射或发射一个null。
        // 通常不发射是更干净的行为，避免向前端发送不必要的空事件。
        if (sourceData == null)
        {
            this.DebugDto.EventEmitted = false;
            return Result.Ok();
        }

        this.DebugDto.EmittedDataPreview = JsonSerializer.Serialize(sourceData, YaeSandBoxJsonHelper.JsonSerializerOptions);

        // 构建发射载荷
        var payload = new EmitPayload(
            Address: this.Config.TargetAddress ?? string.Empty,
            Data: sourceData,
            Mode: this.Config.UpdateMode
        );

        // 调用新的事件发射器回调
        var result = await this.WorkflowRuntimeService.CallbackAsync<IWorkflowEventEmitter>(it => it.EmitAsync(payload));

        if (result.IsSuccess)
        {
            this.DebugDto.EventEmitted = true;
        }

        return result;
    }
}

/// <summary>
/// "发射事件"符文的配置，用于将工作流内部变量的值发送到外部逻辑地址。
/// </summary>
[ClassLabel("📤发射事件")]
internal record EmitEventRuneConfig : AbstractRuneConfig<EmitEventRuneProcessor>
{
    /// <summary>
    /// 要读取并作为数据发射的内部变量的名称。
    /// </summary>
    [Required]
    [Display(
        Name = "源变量名",
        Description = "指定要读取并作为数据发射的内部变量的名称。"
    )]
    public string SourceVariableName { get; init; } = string.Empty;

    /// <summary>
    /// 数据要发射到的外部逻辑地址。
    /// </summary>
    [Display(
        Name = "目标地址",
        Description = "(可空)指定数据要发射到的外部逻辑地址，例如 'ui.main_output' 或 'data.final_summary'。为空则发送到根地址。",
        Prompt = "e.g., ui.main_output"
    )]
    [DefaultValue("")]
    public string? TargetAddress { get; init; } = string.Empty;

    /// <summary>
    /// 决定数据是替换目标地址的现有内容还是附加到其后。
    /// </summary>
    [Required]
    [DefaultValue(UpdateMode.FullSnapshot)]
    [Display(
        Name = "更新模式",
        Description = "全快照模式会替换目标地址的现有内容；增量模式会附加到其后。"
    )]
    [StringOptions([nameof(UpdateMode.FullSnapshot), nameof(UpdateMode.Incremental)], ["全快照", "增量"])]
    public UpdateMode UpdateMode { get; init; } = UpdateMode.FullSnapshot;

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() =>
        // 它消费任意类型的变量，因为发射器可以处理任何可序列化的对象。
        [new(this.SourceVariableName, CoreVarDefs.Any)];

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec() =>
        // 此符文不向工作流内部生产任何变量。
        [];

    /// <inheritdoc />
    protected override EmitEventRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) =>
        new(this, workflowRuntimeService);
}