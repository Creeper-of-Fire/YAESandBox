using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Runtime.Processor;
using YAESandBox.Workflow.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Runtime.Processor.TuumProcessor;

namespace YAESandBox.Workflow.ExactRune.SillyTavern;

internal class SillyTavernRuneProcessor(SillyTavernRuneConfig config, ICreatingContext creatingContext)
    : NormalRuneProcessor<SillyTavernRuneConfig, SillyTavernRuneProcessor.SillyTavernRuneProcessorDebugDto>(config, creatingContext)
{
    /// <inheritdoc />
    public override Task<Result> ExecuteAsync(TuumProcessorContent tuumContent, CancellationToken cancellationToken = default)
    {
        // TODO: [宏扩展范围] 当前宏替换仅作用于预设(Preset)内容。
        // 传入的历史记录(History)和世界书(World Info)中的宏不会被解析。
        // 长期方案: 应当创建一个独立的、通用的 "宏替换符文(Macro Expansion Rune)"，
        // 可以在工作流的任意阶段对提示词列表进行处理，以实现更灵活的控制。

        this.DebugDto.AddLog("SillyTavern符文开始执行。");

        // --- 1. 获取所有输入 ---
        var history = tuumContent.GetTuumVar<List<RoledPromptDto>>(this.Config.HistoryVariableName) ?? [];
        var worldInfoJsons = tuumContent.GetTuumVar<List<string>>(this.Config.WorldInfoJsonsVariableName) ?? [];
        this.DebugDto.HistoryBeforeInjection = new List<RoledPromptDto>(history);
        this.DebugDto.AddLog($"获取到 {history.Count} 条历史记录，{worldInfoJsons.Count} 个世界书。");

        if (!tuumContent.TryGetTuumVar<ThingInfo>(this.Config.PlayerCharacterVariableName, out var playerInfo))
        {
            string errorMsg = $"未能找到玩家角色信息变量: '{this.Config.PlayerCharacterVariableName}'";
            this.DebugDto.AddLog($"错误: {errorMsg}");
            return Result.Fail(errorMsg).AsCompletedTask();
        }

        if (!tuumContent.TryGetTuumVar<ThingInfo>(this.Config.TargetCharacterVariableName, out var targetInfo))
        {
            string errorMsg = $"未能找到目标角色信息变量: '{this.Config.TargetCharacterVariableName}'";
            this.DebugDto.AddLog($"错误: {errorMsg}");
            return Result.Fail(errorMsg).AsCompletedTask();
        }

        this.DebugDto.AddLog($"已加载玩家 '{playerInfo.Name}' 和目标 '{targetInfo.Name}' 的角色信息。");

        // --- 2. 反序列化 ---
        this.DebugDto.AddLog("开始解析预设JSON...");
        var presetResult = SillyTavernDeserializer.DeserializePreset(this.Config.PresetJson);
        if (presetResult.TryGetError(out var error, out var preset))
        {
            this.DebugDto.AddLog($"预设解析失败: {error.ToDetailString()}");
            return error.AsCompletedTask();
        }

        this.DebugDto.AddLog("预设解析成功。");

        var worldBooks = new List<SillyTavernWorldInfo>();
        for (int i = 0; i < worldInfoJsons.Count; i++)
        {
            var result = SillyTavernDeserializer.DeserializeWorldInfo(worldInfoJsons[i]);
            if (result.TryGetValue(out var wb))
            {
                worldBooks.Add(wb);
                this.DebugDto.WorldInfoParsingLogs[i] = "解析成功";
            }
            else if (result.TryGetError(out var wiError))
            {
                this.DebugDto.WorldInfoParsingLogs[i] = $"解析失败: {wiError.ToDetailString()}";
            }
        }

        this.DebugDto.AddLog($"成功解析 {worldBooks.Count} / {worldInfoJsons.Count} 个世界书。");

        // --- 3. 核心处理 ---
        this.DebugDto.AddLog("开始处理世界书激活与递归...");
        var worldInfoProcessResult =
            SillyTavernProcessor.ProcessWorldInfo(
                worldBooks, history,
                this.Config.WorldInfoGlobalSettings.GlobalScanDepth,
                this.Config.WorldInfoGlobalSettings.MaxRecursionDepth
            );
        this.DebugDto.GeneratedWorldInfoBefore = worldInfoProcessResult.WorldInfoBefore;
        this.DebugDto.GeneratedWorldInfoAfter = worldInfoProcessResult.WorldInfoAfter;
        this.DebugDto.WorldInfoInjectionCommands = worldInfoProcessResult.DepthInjections;
        this.DebugDto.AddLog($"世界书处理完成。生成 Before/After 内容，并提取 {worldInfoProcessResult.DepthInjections.Count} 条注入指令。");

        this.DebugDto.AddLog("开始处理预设模板...");
        var presetProcessResult = preset.GetOrderedPrompts().ExtractAndTemplate();
        this.DebugDto.PresetInjectionCommands = presetProcessResult.DepthInjections;
        this.DebugDto.AddLog($"预设处理完成。生成 {presetProcessResult.Template.Count} 个模板项，并提取 {presetProcessResult.DepthInjections.Count} 条注入指令。");

        // --- 4. 变量填充循环 (内部闭包环境) ---
        this.DebugDto.AddLog("开始填充模板变量...");
        var variables = new Dictionary<string, string>();
        var filledTemplateItems = new List<PromptTemplateItem>();

        foreach (var item in presetProcessResult.Template)
        {
            // 将 history 传给 FillTemplate
            var fillResult = item.FillTemplate(variables, playerInfo, targetInfo, history);

            filledTemplateItems.Add(fillResult.FilledItem);

            foreach ((string key, string value) in fillResult.ProducedVariables)
            {
                variables[key] = value;
            }
        }

        this.DebugDto.FinalProducedVariables.Clear();
        foreach (var pair in variables) this.DebugDto.FinalProducedVariables.Add(pair.Key, pair.Value);
        this.DebugDto.AddLog($"变量填充完成。共生成 {variables.Count} 个变量。");

        // --- 5. 最终组装 ---
        this.DebugDto.AddLog("开始最终组装...");
        var finalTemplateResult = presetProcessResult with { Template = filledTemplateItems };
        var finalPrompts =
            SillyTavernProcessor.AssembleFinalPromptList(finalTemplateResult, worldInfoProcessResult, history, playerInfo, targetInfo);
        this.DebugDto.HistoryAfterInjection = finalPrompts.Where(p =>
            history.Contains(p) || this.DebugDto.PresetInjectionCommands.Any(c => c.Content == p.Content) ||
            this.DebugDto.WorldInfoInjectionCommands.Any(c => c.Content == p.Content)).ToList(); // 这是一个近似值
        this.DebugDto.FinalPromptList = finalPrompts;
        this.DebugDto.AddLog($"组装完成。最终生成 {finalPrompts.Count} 条提示词。");

        // --- 6. 设置输出 ---
        // 不将内部变量泄露到外部
        tuumContent.SetTuumVar(this.Config.OutputPromptsVariableName, finalPrompts);

        this.DebugDto.AddLog("所有输出变量已设置。执行成功。");

        return Result.Ok().AsCompletedTask();
    }

    /// <summary>
    /// 用于调试 SillyTavern 符文处理器的详细数据传输对象。
    /// </summary>
    internal record SillyTavernRuneProcessorDebugDto : IRuneProcessorDebugDto
    {
        /// <summary>
        /// 记录整个执行过程中的关键步骤和日志信息。
        /// </summary>
        public List<string> Logs { get; } = [];

        /// <summary>
        /// 记录世界书 JSON 字符串的解析结果。
        /// 键: JSON 字符串的索引，值: 解析成功或失败的信息。
        /// </summary>
        public Dictionary<int, string> WorldInfoParsingLogs { get; } = [];

        /// <summary>
        /// 处理世界书后生成的 WorldInfoBefore 内容。
        /// </summary>
        public string? GeneratedWorldInfoBefore { get; internal set; }

        /// <summary>
        /// 处理世界书后生成的 WorldInfoAfter 内容。
        /// </summary>
        public string? GeneratedWorldInfoAfter { get; internal set; }

        /// <summary>
        /// 从预设中提取的深度注入指令。
        /// </summary>
        public List<DepthInjectionCommand> PresetInjectionCommands { get; internal set; } = [];

        /// <summary>
        /// 从世界书中提取的深度注入指令。
        /// </summary>
        public List<DepthInjectionCommand> WorldInfoInjectionCommands { get; internal set; } = [];

        /// <summary>
        /// 注入到历史记录之前的聊天记录快照。
        /// </summary>
        public List<RoledPromptDto>? HistoryBeforeInjection { get; internal set; }

        /// <summary>
        /// 经过深度注入指令处理后的聊天记录快照。
        /// </summary>
        public List<RoledPromptDto>? HistoryAfterInjection { get; internal set; }

        /// <summary>
        /// 最终通过 {{setvar::...}} 产生并输出的所有变量及其值。
        /// </summary>
        public Dictionary<string, string> FinalProducedVariables { get; } = new();

        /// <summary>
        /// 最终组装完成的提示词列表。
        /// </summary>
        public List<RoledPromptDto>? FinalPromptList { get; internal set; }

        internal void AddLog(string message)
        {
            this.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
        }
    }
}

/// <summary>
/// 一个完整的 SillyTavern 处理器符文的配置。
/// 它整合了预设和世界书，并处理变量填充，以生成最终的提示词列表。
/// </summary>
[ClassLabel("🍻酒馆预设")]
[RuneCategory("提示词处理")]
internal record SillyTavernRuneConfig : AbstractRuneConfig<SillyTavernRuneProcessor>
{
    private const string GroupInputs = "输入变量";
    private const string GroupOutputs = "输出变量";
    private const string GroupSettings = "世界书全局设置";

    /// <summary>
    /// 定义世界书的全局处理设置。
    /// </summary>
    public record WorldInfoSettings
    {
        [InlineGroup(groupName: GroupSettings)]
        [Required]
        [DefaultValue(2)]
        [Display(Name = "全局扫描深度", Description = "默认在历史记录中回溯多少条消息来匹配世界书关键字。")]
        public int GlobalScanDepth { get; init; } = 2;

        [InlineGroup(groupName: GroupSettings)]
        [Required]
        [DefaultValue(5)]
        [Display(Name = "最大递归深度", Description = "世界书条目之间互相激活的最大次数。0 表示无限（内部会设一个安全上限）。")]
        public int MaxRecursionDepth { get; init; } = 5;
    }

    #region Config Properties

    [Required]
    [DefaultValue(AiRuneConfig.PromptsDefaultName)]
    [Display(Name = "输出提示词列表", Description = "处理完成后生成的最终提示词列表的变量名。")]
    public string OutputPromptsVariableName { get; init; } = AiRuneConfig.PromptsDefaultName;

    [InlineGroup(groupName: GroupInputs)]
    [Required]
    [DefaultValue(HistoryAppendRuneConfig.HistoryDefaultName)]
    [Display(Name = "输入历史记录", GroupName = GroupInputs, Description = "要处理的原始聊天记录提示词列表的变量名。[注意：当前版本不会替换历史记录中的宏。]")]
    public string HistoryVariableName { get; init; } = HistoryAppendRuneConfig.HistoryDefaultName;

    [InlineGroup(groupName: GroupInputs)]
    [Required]
    [DefaultValue("worldInfoList")]
    [Display(Name = "世界书JSON列表", Description = "包含多个世界书JSON字符串的列表变量名。[注意：当前版本不会替换世界书内容中的宏。]")]
    public string WorldInfoJsonsVariableName { get; init; } = "worldInfoList";

    [InlineGroup(groupName: GroupInputs)]
    [Required]
    [DefaultValue("playerCharacter")]
    [Display(Name = "玩家角色信息", Description = "用于填充 {{user}} 和 {{persona}}/personaDescription 的玩家角色信息变量名。")]
    public string PlayerCharacterVariableName { get; init; } = "playerCharacter";

    [InlineGroup(groupName: GroupInputs)]
    [Required]
    [DefaultValue("targetCharacter")]
    [Display(Name = "目标角色信息", Description = "用于填充 {{char}} 和 {{description}}/charDescription 的目标角色信息变量名。")]
    public string TargetCharacterVariableName { get; init; } = "targetCharacter";

    [Display(Name = "世界书全局设置", Description = "配置世界书的全局扫描和递归行为。")]
    public WorldInfoSettings WorldInfoGlobalSettings { get; init; } = new();

    [Required(AllowEmptyStrings = true)]
    [RenderWithCustomWidget("SillyTavernPresetEditor")]
    [Display(Name = "SillyTavern 预设 JSON", Description = "在此处粘贴完整的 SillyTavern 预设 JSON 内容。")]
    public string PresetJson { get; init; } = string.Empty;

    #endregion

    /// <inheritdoc />
    protected override SillyTavernRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);

    #region Consumed & Produced Spec

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec()
    {
        return
        [
            new ConsumedSpec(this.HistoryVariableName, CoreVarDefs.PromptList),
            new ConsumedSpec(this.PlayerCharacterVariableName, ExtendVarDefs.ThingInfo),
            new ConsumedSpec(this.TargetCharacterVariableName, ExtendVarDefs.ThingInfo),
            new ConsumedSpec(this.WorldInfoJsonsVariableName, ExtendVarDefs.SillyTavernWorldInfoJsonList) { IsOptional = true }
        ];
    }

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec()
    {
        return [new ProducedSpec(this.OutputPromptsVariableName, CoreVarDefs.PromptList)];
    }

    #endregion
}