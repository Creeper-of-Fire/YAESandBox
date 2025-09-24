using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.AIService.Shared;
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
        public string? FinalReasoning { get; set; }
        public string? FinalContent { get; set; }

        /// <summary>
        /// 记录发射了多少次流式事件
        /// </summary>
        public int StreamingEventsSent { get; set; }
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

        var prompts = tuumProcessorContent.GetTuumVar<List<RoledPromptDto>>(AiRuneConfig.PromptsName) ?? [];
        this.DebugDto.Prompts = prompts;

        var executionResult = aiConfig.IsStream
            ? await this.ExecuteStreamAsync(aiProcessor, prompts, cancellationToken)
            : await this.ExecuteNonStreamAsync(aiProcessor, prompts, cancellationToken);

        if (executionResult.TryGetError(out var error, out var fullResponse))
            return error;

        return this.ProcessFinalResult(tuumProcessorContent, fullResponse.finalReasoning, fullResponse.finalContent);
    }

    /// <summary>
    /// 根据配置，将最终的 Reasoning 和 Content 写入 Tuum 变量中。
    /// </summary>
    private Result ProcessFinalResult(TuumProcessorContent tuumProcessorContent, string finalReasoning, string finalContent)
    {
        this.DebugDto.FinalReasoning = finalReasoning;
        this.DebugDto.FinalContent = finalContent;

        // 1. 如果配置了，则写入独立的思维过程变量
        if (!string.IsNullOrEmpty(this.Config.ReasoningOutputName))
        {
            tuumProcessorContent.SetTuumVar(this.Config.ReasoningOutputName, finalReasoning);
        }

        // 2. 根据配置的格式，决定写入主输出变量的内容
        string finalOutputValue = this.Config.FinalOutputFormat switch
        {
            nameof(AiOutputFormat.ContentWithThinkTag) =>
                new AiStructuredChunk(finalReasoning, finalContent).ToLegacyThinkString(),
            _ => finalContent // 默认 DoubleOutput
        };

        tuumProcessorContent.SetTuumVar(this.Config.AiOutputName, finalOutputValue);

        return Result.Ok();
    }

    private async Task<Result<(string finalReasoning, string finalContent)>> ExecuteStreamAsync
        (IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts, CancellationToken cancellationToken)
    {
        StringBuilder reasoningBuilder = new();
        StringBuilder contentBuilder = new();

        var callBack = new StreamRequestCallBack
        {
            OnChunkReceivedAsync = async chunk =>
            {
                // 1. 累积内部状态
                if (!string.IsNullOrEmpty(chunk.Reasoning))
                    reasoningBuilder.Append(chunk.Reasoning);
                if (!string.IsNullOrEmpty(chunk.Content))
                    contentBuilder.Append(chunk.Content);

                if (chunk.IsEmpty())
                    return Result.Ok();

                Result emitResult;
                if (this.Config.FinalOutputFormat == nameof(AiOutputFormat.ContentWithThinkTag))
                {
                    // 策略A: 合并发射
                    emitResult = await this.EmitMergedStreamChunkAsync(chunk, reasoningBuilder, contentBuilder, cancellationToken);
                }
                else // 默认为 DoubleOutput
                {
                    // 策略B: 双重发射
                    emitResult = await this.EmitDoubleStreamChunkAsync(chunk, reasoningBuilder, contentBuilder, cancellationToken);
                }

                if (emitResult.TryGetError(out var emitError))
                    return emitError;

                this.DebugDto.StreamingEventsSent++;
                return Result.Ok();
            },
            TokenUsage = tokenCount => { this.DebugDto.TokenUsage = tokenCount; }
        };

        var result = await aiProcessor.StreamRequestAsync(prompts, callBack, cancellationToken);

        if (result.TryGetError(out var requestError))
            return requestError;

        // 流式结束后，处理最终状态
        return (reasoningBuilder.ToString(), contentBuilder.ToString());
    }

    /// <summary>
    /// 实现“双重”发射逻辑：分开 thinking 和 content。
    /// </summary>
    private async Task<Result> EmitDoubleStreamChunkAsync(
        AiStructuredChunk chunk,
        StringBuilder reasoningBuilder,
        StringBuilder contentBuilder,
        CancellationToken cancellationToken)
    {
        var payloadMode = Enum.TryParse<UpdateMode>(this.Config.StreamingMode, out var mode) ? mode : UpdateMode.Incremental;

        // 1. 发射思维过程 (如果有)
        if (!string.IsNullOrEmpty(chunk.Reasoning))
        {
            string thinkAddress = $"{this.Config.StreamingTargetAddress ?? string.Empty}.think";

            object reasoningDataToSend = this.Config.StreamingMode == nameof(UpdateMode.Incremental)
                ? chunk.Reasoning
                : reasoningBuilder.ToString();

            var thinkPayload = new EmitPayload(thinkAddress, reasoningDataToSend, payloadMode);
            var result = await this.WorkflowRuntimeService.CallbackAsync<IWorkflowEventEmitter>(it =>
                it.EmitAsync(thinkPayload, cancellationToken));
            if (result.TryGetError(out var error)) return error;
        }

        // 2. 发射主内容 (如果有)
        if (!string.IsNullOrEmpty(chunk.Content))
        {
            object dataToSend = this.Config.StreamingMode == nameof(UpdateMode.Incremental)
                ? chunk.Content
                : contentBuilder.ToString();

            var payload = new EmitPayload(this.Config.StreamingTargetAddress ?? string.Empty, dataToSend, payloadMode);
            var result = await this.WorkflowRuntimeService.CallbackAsync<IWorkflowEventEmitter>(it =>
                it.EmitAsync(payload, cancellationToken));
            if (result.TryGetError(out var error)) return error;
        }

        return Result.Ok();
    }

    /// <summary>
    /// 实现“合并”发射逻辑：发送包含 think 标签的组合字符串。
    /// </summary>
    private async Task<Result> EmitMergedStreamChunkAsync(
        AiStructuredChunk chunk,
        StringBuilder reasoningBuilder,
        StringBuilder contentBuilder,
        CancellationToken cancellationToken)
    {
        // 1. 决定要发送的数据
        object dataToSend = this.Config.StreamingMode == nameof(UpdateMode.Incremental)
            // 增量模式：只发送当前 chunk 转换后的字符串
            ? chunk.ToLegacyThinkString()
            // 全快照模式：使用实时的 reasoningBuilder 和 contentBuilder 来构建完整的当前状态
            : new AiStructuredChunk(reasoningBuilder.ToString(), contentBuilder.ToString()).ToLegacyThinkString();

        // 2. 创建并发送 payload
        var payload = new EmitPayload(this.Config.StreamingTargetAddress ?? string.Empty, dataToSend,
            Enum.TryParse<UpdateMode>(this.Config.StreamingMode, out var mode) ? mode : UpdateMode.Incremental);

        var emitResult = await this.WorkflowRuntimeService.CallbackAsync<IWorkflowEventEmitter>(it =>
            it.EmitAsync(payload, cancellationToken));

        return emitResult;
    }

    private async Task<Result<(string finalReasoning, string finalContent)>> ExecuteNonStreamAsync
        (IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts, CancellationToken cancellationToken)
    {
        string reasoning = string.Empty;
        string content = string.Empty;

        var callBack = new NonStreamRequestCallBack
        {
            OnFinalResponseReceivedAsync = finalResponse =>
            {
                reasoning = finalResponse.Reasoning ?? string.Empty;
                content = finalResponse.Content ?? string.Empty;
                return Task.FromResult(Result.Ok());
            },
            TokenUsage = tokenCount => { this.DebugDto.TokenUsage = tokenCount; }
        };
        var result = await aiProcessor.NonStreamRequestAsync(prompts, callBack, cancellationToken);

        if (result.TryGetError(out var error))
            return error;

        // 非流式结束后，处理最终状态
        return (reasoning, content);
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
    [JsonIgnore]
    public static string PromptsName => PromptsDefaultName;

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
    /// (可选) 用于存储AI思维过程的变量名。
    /// 如果为空，思维过程将被丢弃（除非在流式模式下发送）。
    /// </summary>
    [Display(
        Name = "思维过程变量名 (可选)",
        Description = "指定一个变量名来存储AI的思维过程（<think>标签内的内容）。如果留空，这部分内容在最终输出时会被忽略。"
    )]
    public string? ReasoningOutputName { get; init; }

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
        Description =
            "指定一个逻辑地址，例如 'ui.chat_window'。\n" +
            "AI在流式响应时，会把数据块实时发送到这里。对于地址的解析是外部自行实现的。\n" +
            "如果为空，则发送至某种意义上的根目录。\n" +
            "AI的思维过程会被发送到当前地址下的`think`子路径。"
    )]
    [DefaultValue("")]
    public string? StreamingTargetAddress { get; init; } = string.Empty;

    /// <summary>
    /// 定义内容变量的输出格式。
    /// </summary>
    [Required]
    [DefaultValue(nameof(AiOutputFormat.DoubleOutput))]
    [Display(
        Name = "内容格式",
        Description =
            "决定写入AI输出变量名和发射流式内容的格式。\n" +
            "'双重'则分开发送两者（流式的思维过程会被发送到流式输出地址下的`think`子路径）；\n" +
            "'合并'则发送包含<think>标签的组合字符串。"
    )]
    [StringOptions(
        [nameof(AiOutputFormat.DoubleOutput), nameof(AiOutputFormat.ContentWithThinkTag)],
        ["双重", "合并"]
    )]
    public string FinalOutputFormat { get; init; } = nameof(AiOutputFormat.DoubleOutput);

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
    public override List<ConsumedSpec> GetConsumedSpec() => [new(PromptsName, CoreVarDefs.PromptList)];

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec()
    {
        var produced = new List<ProducedSpec> { new(this.AiOutputName, CoreVarDefs.String) };
        if (!string.IsNullOrEmpty(this.ReasoningOutputName))
        {
            produced.Add(new ProducedSpec(this.ReasoningOutputName, CoreVarDefs.String));
        }

        return produced;
    }

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

/// <summary>
/// 定义 AI 最终输出的格式。
/// </summary>
public enum AiOutputFormat
{
    /// <summary>
    /// 分别输出两者。
    /// </summary>
    DoubleOutput,

    /// <summary>
    /// 将两者合并输出。
    /// </summary>
    ContentWithThinkTag
}