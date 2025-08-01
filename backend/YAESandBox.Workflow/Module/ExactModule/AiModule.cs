using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Namotion.Reflection;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Step;
using static YAESandBox.Workflow.Module.ExactModule.AiModuleProcessor;
using static YAESandBox.Workflow.Step.StepProcessor;

namespace YAESandBox.Workflow.Module.ExactModule;

/// <summary>
/// Ai调用模块，Ai的配置保存在外部的Step，并且注入到执行函数中，所以这里只需要保存一些临时的调试信息到生成它的<see cref="AiModuleConfig"/>里面。
/// </summary>
/// <param name="onChunkReceivedScript"></param>
internal class AiModuleProcessor(Action<string> onChunkReceivedScript, AiModuleConfig config)
    : IWithDebugDto<AiModuleProcessorDebugDto>, INormalModule
{
    /// <inheritdoc />
    public AiModuleProcessorDebugDto DebugDto { get; } = new();

    internal class AiModuleProcessorDebugDto : IModuleProcessorDebugDto
    {
        public IList<RoledPromptDto> Prompts { get; init; } = [];
        public int TokenUsage { get; set; } = 0;
    }

    // TODO 这里是回调函数，应该由脚本完成
    private Action<string> OnChunkReceivedScript { get; } = onChunkReceivedScript;
    private AiModuleConfig Config { get; } = config;

    /// <summary>
    /// AI模块的运行
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


    private static async Task<Result> PrepareAndExecuteAiModule(
        StepProcessorContent stepProcessorContent,
        AiModuleProcessor aiModule,
        CancellationToken cancellationToken = default)
    {
        var config = aiModule.Config;
        var workflowRuntimeService = stepProcessorContent.WorkflowRuntimeService;
        if (config.AiConfiguration == null)
            return NormalError.Conflict($"步骤 {workflowRuntimeService} 没有配置AI信息，所以无法执行AI模块。");
        var aiProcessor = workflowRuntimeService.MasterAiService.CreateAiProcessor(config.AiConfiguration);
        var prompt = stepProcessorContent.Prompts;
        var result = await aiModule.ExecuteAsync(aiProcessor,
            prompt,
            config.IsStream,
            cancellationToken);
        if (result.TryGetError(out var error, out string? value))
            return error;
        stepProcessorContent.OutputVar(AiModuleConfig.AiOutputName, value);
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
    public Task<Result> ExecuteAsync(StepProcessorContent stepProcessorContent,
        CancellationToken cancellationToken = default) =>
        PrepareAndExecuteAiModule(stepProcessorContent, this, cancellationToken);
}

[Behind(typeof(PromptGenerationModuleConfig))]
[ClassLabel("🤖AI调用")]
internal record AiModuleConfig : AbstractModuleConfig<AiModuleProcessor>
{
    internal const string PromptsName = nameof(StepProcessorContent.Prompts);
    internal const string AiOutputName = "AiOutput";

    /// <inheritdoc />
    [Required]
    [ReadOnly(true)]
    [HiddenInForm(true)]
    [Display(Name = "配置名称", Description = "模块的配置名称，用于在界面上显示。")]
    public override string Name { get; init; } = string.Empty;

    /// <summary>
    /// 是否为流式传输
    /// </summary>
    [Required]
    public required bool IsStream { get; init; } = false;

    /// <summary>
    /// AI 服务配置。
    /// </summary>
    [RenderAsCustomObjectWidget("AiConfigEditorWidget")]
    [Display(Name = "AI 服务配置", Description = "为该AI调用模块配置AI服务、模型和流式选项。")]
    public AbstractAiProcessorConfig? AiConfiguration { get; init; } = null;


    /// <inheritdoc />
    internal override List<string> GetConsumedVariables() => [PromptsName];

    /// <inheritdoc />
    internal override List<string> GetProducedVariables() => [AiOutputName];

    protected override AiModuleProcessor ToCurrentModule(WorkflowRuntimeService workflowRuntimeService) =>
        new(s => { _ = workflowRuntimeService.Callback<IWorkflowCallbackDisplayUpdate>(it => it.DisplayUpdateAsync(s)); }, this);
}