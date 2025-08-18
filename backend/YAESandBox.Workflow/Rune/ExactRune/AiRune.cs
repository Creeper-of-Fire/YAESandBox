using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.Core.Abstractions;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Rune.ExactRune.AiRuneProcessor;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Workflow.Rune.ExactRune;

/// <summary>
/// Ai调用符文，Ai的配置保存在外部的Tuum，并且注入到执行函数中，所以这里只需要保存一些临时的调试信息到生成它的<see cref="AiRuneConfig"/>里面。
/// </summary>
/// <param name="config"></param>
/// <param name="workflowRuntimeService"></param>
internal class AiRuneProcessor(AiRuneConfig config, WorkflowRuntimeService workflowRuntimeService)
    : INormalRune<AiRuneConfig, AiRuneProcessorDebugDto>
{
    /// <inheritdoc />
    public AiRuneConfig Config { get; } = config;

    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

    /// <inheritdoc />
    public AiRuneProcessorDebugDto DebugDto { get; } = new();

    internal class AiRuneProcessorDebugDto : IRuneProcessorDebugDto
    {
        public IList<RoledPromptDto> Prompts { get; set; } = [];
        public int TokenUsage { get; set; } = 0;

        /// <summary>
        /// 记录最终的完整响应
        /// </summary>
        public string? FinalResponse { get; set; }

        /// <summary>
        /// 记录发射了多少次流式事件
        /// </summary>
        public int StreamingEventsSent { get; set; }
    }

    /// <summary>
    /// AI符文的运行
    /// </summary>
    /// <param name="aiProcessor">从AI配置中实例化的运行时对象</param>
    /// <param name="prompts">提示词</param>
    /// <param name="isStream">是否流式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回AI最终组装完成的输出</returns>
    public Task<Result<string>> ExecuteAsync
        (IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts, bool isStream, CancellationToken cancellationToken = default)
    {
        return isStream switch
        {
            true => this.ExecuteStreamAsync(aiProcessor, prompts, cancellationToken),
            false => this.ExecuteNonStreamAsync(aiProcessor, prompts, cancellationToken)
        };
    }


    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent,
        CancellationToken cancellationToken = default)
    {
        var aiConfig = this.Config.AiConfiguration;
        var workflowRuntimeService = tuumProcessorContent.WorkflowRuntimeService;

        if (aiConfig.SelectedAiRuneType == null || aiConfig.AiProcessorConfigUuid == null)
            return NormalError.Conflict($"枢机 {workflowRuntimeService} 没有配置AI信息，所以无法执行AI符文。");

        var aiProcessor = workflowRuntimeService.AiService.CreateAiProcessor(
            aiConfig.AiProcessorConfigUuid,
            aiConfig.SelectedAiRuneType);
        if (aiProcessor == null)
            return NormalError.Conflict($"未找到 AI 配置 {aiConfig.AiProcessorConfigUuid}配置下的类型：{aiConfig.SelectedAiRuneType}");

        var prompts = tuumProcessorContent.GetTuumVar<List<RoledPromptDto>>(this.Config.PromptsName) ?? [];
        this.DebugDto.Prompts = prompts;

        var executionResult = aiConfig.IsStream
            ? await this.ExecuteStreamAsync(aiProcessor, prompts, cancellationToken)
            : await this.ExecuteNonStreamAsync(aiProcessor, prompts, cancellationToken);

        if (executionResult.TryGetError(out var error, out string? fullResponse))
            return error;

        this.DebugDto.FinalResponse = fullResponse;
        tuumProcessorContent.SetTuumVar(this.Config.AiOutputName, fullResponse);
        return Result.Ok();
    }

    private async Task<Result<string>> ExecuteStreamAsync
        (IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts, CancellationToken cancellationToken = default)
    {
        var responseBuilder = new System.Text.StringBuilder();
        var callBack = new StreamRequestCallBack
        {
            OnChunkReceivedAsync = async chunk =>
            {
                // 1. 内部状态累积
                responseBuilder.Append(chunk);

                object dataToSend = this.Config.StreamingMode == nameof(UpdateMode.Incremental)
                    ? chunk
                    : responseBuilder.ToString();

                var payload = new EmitPayload(this.Config.StreamingTargetAddress ?? string.Empty, dataToSend,
                    Enum.TryParse<UpdateMode>(this.Config.StreamingMode, out var updateMode) ? updateMode : UpdateMode.Incremental);

                var emitterResult =
                    await this.WorkflowRuntimeService.CallbackAsync<IWorkflowEventEmitter>(it => it.EmitAsync(payload, cancellationToken));
                if (emitterResult.TryGetError(out var emitterError))
                    return emitterError;
                this.DebugDto.StreamingEventsSent++;
                return Result.Ok();
            },
            TokenUsage = tokenCount => { this.DebugDto.TokenUsage = tokenCount; }
        };

        var result = await aiProcessor.StreamRequestAsync(prompts, callBack, cancellationToken);
        if (result.TryGetError(out var error))
            return error;
        return responseBuilder.ToString();
    }

    private async Task<Result<string>> ExecuteNonStreamAsync
        (IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts, CancellationToken cancellationToken = default)
    {
        string finalResponse = "";

        // 准备回调对象
        var callBack = new NonStreamRequestCallBack
        {
            OnFinalResponseReceivedAsync = response =>
            {
                finalResponse = response;
                return Task.FromResult(Result.Ok());
            },
            TokenUsage = tokenCount => { this.DebugDto.TokenUsage = tokenCount; }
        };

        var result = await aiProcessor.NonStreamRequestAsync(prompts, callBack, cancellationToken: cancellationToken);
        if (result.TryGetError(out var error))
            return error;
        return finalResponse;
    }
}

[Behind(typeof(PromptGenerationRuneConfig))]
[ClassLabel("🤖AI调用")]
internal record AiRuneConfig : AbstractRuneConfig<AiRuneProcessor>
{
    internal const string PromptsDefaultName = "Prompts";
    internal const string AiOutputDefaultName = "AiOutput";

    /// <summary>
    /// 输入的提示词列表变量的名称
    /// </summary>
    [Required]
    [DefaultValue(PromptsDefaultName)]
    [Display(
        Name = "提示词列表变量名",
        Description = "输入的提示词列表变量的名称。"
    )]
    public string PromptsName { get; init; } = PromptsDefaultName;

    /// <summary>
    /// 输出的AI输出变量的名称
    /// </summary>
    [Required]
    [DefaultValue(AiOutputDefaultName)]
    [Display(
        Name = "AI输出变量名",
        Description = "输出的AI输出变量的名称。"
    )]
    public string AiOutputName { get; init; } = AiOutputDefaultName;

    /// <summary>
    /// AI 服务配置。
    /// </summary>
    [RenderAsCustomObjectWidget("AiConfigEditorWidget")]
    [Display(Name = "AI 服务配置", Description = "为该AI调用符文配置AI服务、模型和流式选项。")]
    public RuneAiConfig AiConfiguration { get; init; } = new()
    {
        IsStream = false
    };

    /// <summary>
    /// (可空) AI的流式输出块将实时发送到该地址。
    /// </summary>
    [Display(
        Name = "流式输出地址 (可空)",
        Description = "指定一个逻辑地址，例如 'ui.chat_window'。AI在流式响应时，会把数据块实时发送到这里。对于地址的解析是外部自行实现的。如果为空，则发送至某种意义上的根目录。"
    )]
    [DefaultValue("")]
    public string? StreamingTargetAddress { get; init; } = string.Empty;

    /// <summary>
    /// 当启用流式输出时，决定发送到外部的数据是增量还是全量。
    /// </summary>
    [Required]
    [DefaultValue(nameof(UpdateMode.Incremental))]
    [Display(
        Name = "流式更新模式",
        Description = "增量模式只发送新数据块，性能好；全快照模式每次都发送完整内容，UI逻辑更简单。"
    )]
    [StringOptions([nameof(UpdateMode.Incremental), nameof(UpdateMode.FullSnapshot)], ["增量", "全快照"])]
    public string StreamingMode { get; init; } = nameof(UpdateMode.Incremental);

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() => [new(this.PromptsName, CoreVarDefs.PromptList)];

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec() => [new(this.AiOutputName, CoreVarDefs.String)];

    protected override AiRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) => new(this, workflowRuntimeService);
}

/// <summary>
/// 符文本身的 AI 配置。
/// </summary>
public record RuneAiConfig
{
    /// <summary>AI服务的配置的UUID</summary>
    public string? AiProcessorConfigUuid { get; init; }

    /// <summary>当前选中的AI模型的类型名</summary>
    public string? SelectedAiRuneType { get; init; }

    /// <summary>是否为流式传输</summary>
    [Required]
    public bool IsStream { get; init; } = false;
}