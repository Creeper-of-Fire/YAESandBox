using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Rune.ExactRune.HistoryAppendRuneProcessor;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Workflow.Rune.ExactRune;

/// <summary>
/// 历史记录追加符文处理器。
/// 将一个提示词列表（历史记录）追加到另一个提示词列表的末尾。
/// </summary>
/// <param name="workflowRuntimeService"><see cref="WorkflowRuntimeService"/></param>
/// <param name="config">符文配置。</param>
internal class HistoryAppendRuneProcessor(WorkflowRuntimeService workflowRuntimeService, HistoryAppendRuneConfig config)
    : INormalRune<HistoryAppendRuneConfig, HistoryAppendRuneProcessorDebugDto>
{
    /// <inheritdoc />
    public HistoryAppendRuneConfig Config { get; } = config;

    /// <inheritdoc />
    public HistoryAppendRuneProcessorDebugDto DebugDto { get; } = new();

    /// <summary>
    /// 执行历史记录追加流程。
    /// </summary>
    /// <param name="tuumProcessorContent">枢机执行的上下文内容。</param>
    /// <param name="cancellationToken"></param>
    /// <returns>执行结果。</returns>
    public Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        this.DebugDto.HistoryVariableName = this.Config.HistoryVariableName;
        this.DebugDto.PromptsVariableName = this.Config.PromptsVariableName;

        // 1. 获取历史记录列表 (必需)
        var historyList = tuumProcessorContent.GetTuumVar<ImmutableList<RoledPromptDto>>(this.Config.HistoryVariableName);
        if (historyList == null)
            return Result.Fail($"必须的历史记录变量 '{this.Config.HistoryVariableName}' 未提供、类型错误或为空。").AsCompletedTask();

        this.DebugDto.HistoryPromptCount = historyList.Count;

        // 2. 获取目标提示词列表 (可选，如果不存在则视为空列表)
        var promptsList = (tuumProcessorContent.GetTuumVar<ImmutableList<RoledPromptDto>>(this.Config.PromptsVariableName) ?? []).ToList();
        this.DebugDto.OriginalPromptCount = promptsList.Count;

        // 3. 执行追加操作
        promptsList.AddRange(historyList);
        this.DebugDto.FinalPromptCount = promptsList.Count;

        // 4. 将合并后的列表设置回上下文
        tuumProcessorContent.SetTuumVar(this.Config.PromptsVariableName, promptsList);

        this.DebugDto.LogOperation(
            $"成功从 '{this.Config.HistoryVariableName}' 添加 {this.DebugDto.HistoryPromptCount} 项历史记录到 '{this.Config.PromptsVariableName}'。");

        return Result.Ok().AsCompletedTask();
    }

    /// <summary>
    /// 历史记录追加符文处理器的调试数据传输对象。
    /// </summary>
    internal record HistoryAppendRuneProcessorDebugDto : IRuneProcessorDebugDto
    {
        /// <summary>
        /// 配置的历史记录变量名。
        /// </summary>
        public string HistoryVariableName { get; internal set; } = string.Empty;

        /// <summary>
        /// 配置的目标提示词变量名。
        /// </summary>
        public string PromptsVariableName { get; internal set; } = string.Empty;

        /// <summary>
        /// 从历史记录中读取到的提示词数量。
        /// </summary>
        public int HistoryPromptCount { get; internal set; }

        /// <summary>
        /// 追加操作前，目标提示词列表中的项目数量。
        /// </summary>
        public int OriginalPromptCount { get; internal set; }

        /// <summary>
        /// 追加操作后，目标提示词列表中的总项目数量。
        /// </summary>
        public int FinalPromptCount { get; internal set; }

        /// <summary>
        /// 操作日志。
        /// </summary>
        public List<string> OperationLogs { get; } = [];

        /// <summary>
        /// 添加一条操作日志。
        /// </summary>
        /// <param name="logEntry">日志条目。</param>
        public void LogOperation(string logEntry)
        {
            this.OperationLogs.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {logEntry}");
        }
    }
}

/// <summary>
/// 历史记录追加符文的配置。
/// </summary>
[InFrontOf(typeof(AiRuneConfig))]
[ClassLabel("📜历史追加")]
internal record HistoryAppendRuneConfig : AbstractRuneConfig<HistoryAppendRuneProcessor>
{
    public const string HistoryDefaultName = "History";

    /// <summary>
    /// 包含历史对话的提示词列表变量的名称。
    /// </summary>
    [Required]
    [DefaultValue(HistoryDefaultName)]
    [Display(
        Name = "历史记录变量名",
        Description = "指定包含要追加的历史记录的提示词列表变量。此变量必须存在。"
    )]
    public string HistoryVariableName { get; init; } = HistoryDefaultName;

    /// <summary>
    /// 将被追加历史记录的目标提示词列表变量的名称。
    /// </summary>
    [Required]
    [DefaultValue(AiRuneConfig.PromptsDefaultName)]
    [Display(
        Name = "目标提示词变量名",
        Description = "指定将接收历史记录的目标提示词列表变量。如果不存在，将创建一个新的列表。"
    )]
    public string PromptsVariableName { get; init; } = AiRuneConfig.PromptsDefaultName;

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() =>
    [
        // 历史记录是必需的输入
        new(this.HistoryVariableName, CoreVarDefs.PromptList),
        // 目标提示词列表是可选的，如果不存在，我们会创建它
        new(this.PromptsVariableName, CoreVarDefs.PromptList) { IsOptional = true }
    ];

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec() =>
    [
        // 符文会生成（或覆盖）目标提示词列表
        new(this.PromptsVariableName, CoreVarDefs.PromptList)
    ];

    /// <inheritdoc />
    protected override HistoryAppendRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) =>
        new(workflowRuntimeService, this);
}