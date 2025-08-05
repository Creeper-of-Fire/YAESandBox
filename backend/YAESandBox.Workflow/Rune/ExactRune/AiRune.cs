using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.Core.Abstractions;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Rune.ExactRune.AiRuneProcessor;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Workflow.Rune.ExactRune;

/// <summary>
/// Ai调用符文，Ai的配置保存在外部的Tuum，并且注入到执行函数中，所以这里只需要保存一些临时的调试信息到生成它的<see cref="AiRuneConfig"/>里面。
/// </summary>
/// <param name="onChunkReceivedScript"></param>
/// <param name="config"></param>
internal class AiRuneProcessor(Action<string> onChunkReceivedScript, AiRuneConfig config)
    : IProcessorWithDebugDto<AiRuneProcessorDebugDto>, INormalRune
{
    /// <inheritdoc />
    public AiRuneProcessorDebugDto DebugDto { get; } = new();

    internal class AiRuneProcessorDebugDto : IRuneProcessorDebugDto
    {
        public IList<RoledPromptDto> Prompts { get; init; } = [];
        public int TokenUsage { get; set; } = 0;
    }

    // TODO 这里是回调函数，应该由脚本完成
    private Action<string> OnChunkReceivedScript { get; } = onChunkReceivedScript;
    private AiRuneConfig Config { get; } = config;

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


    private static async Task<Result> PrepareAndExecuteAiRune(
        TuumProcessorContent tuumProcessorContent,
        AiRuneProcessor aiRune,
        CancellationToken cancellationToken = default)
    {
        var aiConfig = aiRune.Config.AiConfiguration;
        var workflowRuntimeService = tuumProcessorContent.WorkflowRuntimeService;
        
        if (aiConfig.SelectedAiRuneType == null || aiConfig.AiProcessorConfigUuid == null)
            return NormalError.Conflict($"祝祷 {workflowRuntimeService} 没有配置AI信息，所以无法执行AI符文。");
        
        var aiProcessor = workflowRuntimeService.AiService.CreateAiProcessor(
            aiConfig.AiProcessorConfigUuid,
            aiConfig.SelectedAiRuneType);
        if (aiProcessor == null)
            return NormalError.Conflict($"未找到 AI 配置 {aiConfig.AiProcessorConfigUuid}配置下的类型：{aiConfig.SelectedAiRuneType}");

        var prompt = tuumProcessorContent.GetTuumVar<List<RoledPromptDto>>(aiRune.Config.PromptsName) ?? [];
        var result = await aiRune.ExecuteAsync(aiProcessor,
            prompt,
            aiConfig.IsStream,
            cancellationToken);
        if (result.TryGetError(out var error, out string? value))
            return error;
        
        tuumProcessorContent.SetTuumVar(aiRune.Config.AiOutputName, value);
        return Result.Ok();
    }

    private async Task<Result<string>> ExecuteStreamAsync
        (IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts, CancellationToken cancellationToken = default)
    {
        string fullAiReturn = "";
        var result = await aiProcessor.StreamRequestAsync(prompts, new StreamRequestCallBack
        {
            OnChunkReceived = chunk =>
            {
                fullAiReturn += chunk;
                this.OnChunkReceivedScript(chunk);
            }
        }, cancellationToken);
        if (result.TryGetError(out var error))
            return error;
        return fullAiReturn;
    }

    private async Task<Result<string>> ExecuteNonStreamAsync
        (IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts, CancellationToken cancellationToken = default)
    {
        var result = await aiProcessor.NonStreamRequestAsync(prompts, cancellationToken: cancellationToken);
        if (result.TryGetError(out var error, out string? value))
            return error;
        this.OnChunkReceivedScript(value);
        return value;
    }

    /// <inheritdoc />
    public Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent,
        CancellationToken cancellationToken = default) =>
        PrepareAndExecuteAiRune(tuumProcessorContent, this, cancellationToken);
}

[Behind(typeof(PromptGenerationRuneConfig))]
[ClassLabel("🤖AI调用")]
internal record AiRuneConfig : AbstractRuneConfig<AiRuneProcessor>
{
    internal const string PromptsDefaultName = "Prompts";
    internal const string AiOutputDefaultName = "AiOutput";

    /// <inheritdoc />
    [Required]
    [ReadOnly(true)]
    [HiddenInForm(true)]
    [Display(Name = "配置名称", Description = "符文的配置名称，用于在界面上显示。")]
    public override string Name { get; init; } = string.Empty;

    /// <summary>
    /// 输入的提示词列表变量的名称
    /// </summary>
    [Required]
    [DefaultValue(PromptsDefaultName)]
    public string PromptsName { get; init; } = PromptsDefaultName;

    /// <summary>
    /// 输出的AI输出变量的名称
    /// </summary>
    [Required]
    [DefaultValue(AiOutputDefaultName)]
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


    /// <inheritdoc />
    public override List<string> GetConsumedVariables() => [PromptsDefaultName];

    /// <inheritdoc />
    public override List<string> GetProducedVariables() => [AiOutputDefaultName];

    protected override AiRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) =>
        new(s => { _ = workflowRuntimeService.Callback<IWorkflowCallbackDisplayUpdate>(it => it.DisplayUpdateAsync(s)); }, this);
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
    public required bool IsStream { get; init; } = false;
}