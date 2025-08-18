using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Rune.ExactRune.PromptGenerationRuneProcessor;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Workflow.Rune.ExactRune;

/// <summary>
/// 提示词生成符文处理器。
/// 根据配置的模板和上下文数据，生成一个RoledPromptDto。
/// </summary>
/// <param name="workflowRuntimeService"><see cref="WorkflowRuntimeService"/></param>
/// <param name="config">符文配置。</param>
internal partial class PromptGenerationRuneProcessor(WorkflowRuntimeService workflowRuntimeService, PromptGenerationRuneConfig config)
    : INormalRune<PromptGenerationRuneConfig, PromptGenerationRuneProcessorDebugDto>
{
    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

    /// <inheritdoc />
    public PromptGenerationRuneConfig Config { get; init; } = config;

    /// <inheritdoc />
    public PromptGenerationRuneProcessorDebugDto DebugDto { get; init; } = new()
    {
        OriginalTemplate = config.Template,
        ConfiguredRole = PromptRoleTypeExtension.ToPromptRoleType(config.RoleType),
        ConfiguredPromptName = config.PromptNameInAiModel,
    };

    /// <summary>
    /// 启动枢机流程
    /// </summary>
    /// <param name="tuumProcessorContent">枢机执行的上下文内容。</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        string substitutedContent = this.SubstitutePlaceholdersAsync(this.Config.Template, tuumProcessorContent);
        this.DebugDto.FinalPromptContent = substitutedContent;

        var newPrompt = new RoledPromptDto
        {
            Role = PromptRoleTypeExtension.ToPromptRoleType(this.Config.RoleType),
            Content = substitutedContent,
            Name = this.Config.PromptNameInAiModel ?? string.Empty
        };
        this.DebugDto.GeneratedPrompt = newPrompt;

        var prompts = (tuumProcessorContent.GetTuumVar<ImmutableList<RoledPromptDto>>(this.Config.PromptsName) ?? []).ToList();

        if (prompts.Count == 0)
        {
            // 如果列表为空，直接添加新提示词
            this.DebugDto.FinalAction = "在一个空列表中添加第一个提示词。";
            prompts.Add(newPrompt);
            tuumProcessorContent.SetTuumVar(this.Config.PromptsName, prompts);

            return Task.FromResult(Result.Ok());
        }

        // 计算目标索引
        // 0 = 最后一个, 1 = 倒数第二个, etc.
        int targetIndex = prompts.Count - 1 - this.Config.InsertionDepth;
        // 如果计算出的索引小于0（即深度超过了列表长度），则定位到第一个元素
        if (targetIndex < 0)
            targetIndex = 0;
        this.DebugDto.TargetIndex = targetIndex;

        var targetPrompt = prompts[targetIndex];

        // 条件：列表不为空，且最后一个提示词的角色和名称与新提示词完全相同
        if (this.Config.IsAppendMode &&
            targetPrompt.Role == newPrompt.Role &&
            targetPrompt.Name == newPrompt.Name)
        {
            // 执行追加模式
            this.DebugDto.IsAppendMode = true;


            // 创建一个新的 RoledPromptDto 以替换最后一个元素，内容是合并后的
            // 这比直接修改 lastPrompt.Content 更安全，特别是当 RoledPromptDto 是 record 或 struct 时
            string finalContent;
            if (this.Config.InsertionPosition == nameof(InsertionPositionEnum.After))
            {
                this.DebugDto.FinalAction = $"为目标索引为 {targetIndex} 的提示词添加后置内容。";
                finalContent = $"{targetPrompt.Content}\n{newPrompt.Content}";
            }
            else
            {
                this.DebugDto.FinalAction = $"为目标索引为 {targetIndex} 的提示词添加前置内容。";
                finalContent = $"{newPrompt.Content}\n{targetPrompt.Content}";
            }

            var updatedTargetPrompt = new RoledPromptDto
            {
                Role = targetPrompt.Role,
                Name = targetPrompt.Name,
                Content = finalContent,
            };

            prompts[targetIndex] = updatedTargetPrompt;
        }
        else
        {
            // 执行插入模式
            this.DebugDto.IsAppendMode = false;
            if (this.Config.InsertionPosition == nameof(InsertionPositionEnum.After))
            {
                this.DebugDto.FinalAction = $"在目标索引为 {targetIndex} 的提示词之后插入新提示词。";
                // 如果目标是最后一个元素，Insert(index + 1) 等同于 Add()
                prompts.Insert(targetIndex + 1, newPrompt);
            }
            else
            {
                this.DebugDto.FinalAction = $"在目标索引为 {targetIndex} 的提示词之前插入新提示词。";
                prompts.Insert(targetIndex, newPrompt);
            }
        }

        tuumProcessorContent.SetTuumVar(this.Config.PromptsName, prompts);

        return Task.FromResult(Result.Ok());
    }


    private string SubstitutePlaceholdersAsync(
        string template,
        TuumProcessorContent tuumContent)
    {
        var resolvedValues = new Dictionary<string, string?>();

        var uniquePlaceholderNames = PromptGenerationRuneConfig.PlaceholderRegex().Matches(template)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        foreach (string placeholderName in uniquePlaceholderNames)
        {
            string? value = null;
            bool found = false;

            // 尝试从 TuumVariable (dynamic, 假设其内部是 IDictionary<string, object>)
            try
            {
                if (tuumContent.TuumVariable is IDictionary<string, object> tuumInputDict &&
                    tuumInputDict.TryGetValue(placeholderName, out object? tuumValObj))
                {
                    value = tuumValObj.ToString();
                    found = true;
                }
            }
            catch (Exception ex)
            {
                // 记录尝试从TuumInput获取时的潜在错误到调试信息
                this.DebugDto.AddResolutionAttemptLog($"尝试从 TuumVariable 获取 '{placeholderName}' 失败: {ex.Message}");
            }


            if (found)
            {
                resolvedValues[placeholderName] = value;
                this.DebugDto.ResolvedPlaceholdersWithValue[placeholderName] = value ?? "[null]";
            }
            else
            {
                // 如果未找到，则替换为空字符串 (根据用户要求隐式处理)
                resolvedValues[placeholderName] = string.Empty;
                this.DebugDto.UnresolvedPlaceholders.Add(placeholderName);
                this.DebugDto.AddResolutionAttemptLog($"占位符 '{placeholderName}' 未在任何来源中找到，将替换为空字符串。");
            }
        }

        // 执行替换
        string resultText = template;
        foreach (var kvp in resolvedValues)
        {
            // 使用 Regex.Escape 对占位符名称进行转义，以防包含正则表达式特殊字符
            // 但由于我们是从 {} 中提取的，通常不需要，除非占位符名称本身很奇怪
            // 为了简单起见，这里直接替换，假设占位符名称是常规文本
            resultText = resultText.Replace($"[[{kvp.Key}]]", kvp.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return resultText;
    }


    /// <summary>
    /// 提示词生成符文处理器的调试数据传输对象。
    /// </summary>
    internal class PromptGenerationRuneProcessorDebugDto : IRuneProcessorDebugDto
    {
        /// <summary>
        /// 符文配置中原始的提示词模板。
        /// </summary>
        public string OriginalTemplate { get; internal set; } = string.Empty;

        /// <summary>
        /// 符文配置中指定的角色类型。
        /// </summary>
        public PromptRoleType ConfiguredRole { get; internal set; }

        /// <summary>
        /// 符文配置中指定的AI模型中的提示词名称。
        /// </summary>
        public string? ConfiguredPromptName { get; internal set; }

        /// <summary>
        /// 计算出的目标操作索引。仅当列表不为空时有效。
        /// </summary>
        public int? TargetIndex { get; internal set; }

        /// <summary>
        /// 描述最终执行的操作的字符串。
        /// </summary>
        public string FinalAction { get; internal set; } = string.Empty;

        /// <summary>
        /// 经过占位符替换后的最终提示词内容。
        /// </summary>
        public string FinalPromptContent { get; internal set; } = string.Empty;

        /// <summary>
        /// 指示最终操作是追加内容到上一个提示词 (true) 还是添加一个新的提示词项 (false)。
        /// </summary>
        public bool IsAppendMode { get; internal set; }

        /// <summary>
        /// 记录已解析并成功替换的占位符及其值。
        /// 键: 占位符名称 (不带花括号)
        /// 值: 替换的文本 (如果原始值为null，则为 "[null]")
        /// </summary>
        public Dictionary<string, string> ResolvedPlaceholdersWithValue { get; } = [];

        /// <summary>
        /// 记录在模板中找到但未能解析到值的占位符列表。
        /// </summary>
        public List<string> UnresolvedPlaceholders { get; } = [];

        /// <summary>
        /// 详细的占位符解析尝试日志。
        /// </summary>
        public List<string> ResolutionAttemptLogs { get; } = [];

        /// <summary>
        /// 最终生成的提示词对象。
        /// </summary>
        public RoledPromptDto? GeneratedPrompt { get; internal set; }

        /// <summary>
        /// 添加一条解析尝试日志。
        /// </summary>
        /// <param name="logEntry">日志条目。</param>
        public void AddResolutionAttemptLog(string logEntry)
        {
            this.ResolutionAttemptLogs.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {logEntry}");
        }
    }
}

internal partial record PromptGenerationRuneConfig
{
    // 使用 Regex.Matches 获取所有唯一的占位符名称
    // 使用 lookahead 和 lookbehind 来确保我们只匹配 [[}} 包裹的内容，并且不能处理嵌套 [[}} 的情况
    // 简单正则：\{\{([^\{\}]+?)\}\} 匹配非贪婪的、不包含花括号的内容
    [GeneratedRegex(@"\[\[([^\[\]]+?)\]\]")]
    internal static partial Regex PlaceholderRegex();

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec()
    {
        var namesSpec = PlaceholderRegex().Matches(this.Template)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Select(n => new ConsumedSpec(n, CoreVarDefs.String))
            .ToList();
        namesSpec.Add(new ConsumedSpec(this.PromptsName, CoreVarDefs.PromptList) { IsOptional = true });
        return namesSpec;
    }

    public override List<ProducedSpec> GetProducedSpec() => [new(this.PromptsName, CoreVarDefs.PromptList)];
}

/// <summary>
/// 提示词生成符文的配置。
/// </summary>
[InFrontOf(typeof(AiRuneConfig))]
[ClassLabel("✍️提示词")]
internal partial record PromptGenerationRuneConfig : AbstractRuneConfig<PromptGenerationRuneProcessor>
{
    /// <summary>
    /// 使用的提示词列表变量的名称（若存在则在列表中添加，若不存在则创建）。
    /// </summary>
    [Required]
    [DefaultValue(AiRuneConfig.PromptsDefaultName)]
    [Display(
        Name = "提示词列表变量名",
        Description = "使用的提示词列表变量的名称（若存在则在列表中添加，若不存在则创建）。"
    )]
    public string PromptsName { get; init; } = AiRuneConfig.PromptsDefaultName;

    /// <summary>
    /// 生成的提示词的角色类型 (System, User, Assistant)。
    /// </summary>
    [Required]
    [Display(
        Name = "提示词角色类型",
        Description = "选择此提示词在对话历史中扮演的角色。"
    )]
    [StringOptions(
        [
            nameof(PromptRoleType.System),
            nameof(PromptRoleType.User),
            nameof(PromptRoleType.Assistant)
        ],
        [
            "系统",
            "用户",
            "AI助手"
        ]
    )]
    public string RoleType { get; init; } = nameof(PromptRoleType.System);

    /// <summary>
    /// 在某些AI模型中，可以为提示词角色指定一个名称 (例如，Claude中的User/Assistant名称)。
    /// </summary>
    [Display(
        Name = "提示词角色名",
        Description = "为提示词角色指定一个具体名称，可以让某些模型更好的区分不同的用户或助手，对于部分高级模型有用。",
        Prompt = "例如：'DeepSeek' 或 'はちみ'"
    )]
    public string? PromptNameInAiModel { get; init; }

    /// <summary>
    /// 提示词插入的深度。0代表在列表末尾操作，1代表在倒数第二个提示词附近操作，以此类推。如果深度超出范围，则定位到列表的第一个提示词。
    /// </summary>
    [Required]
    [DefaultValue(0)]
    [Display(
        Name = "插入深度",
        Description = "0=在列表末尾操作，1=在倒数第二个提示词附近操作，以此类推。如果深度超出范围，则定位到列表的第一个提示词。"
    )]
    public int InsertionDepth { get; init; } = 0;

    /// <summary>
    /// 决定新提示词是插入到目标提示词之前还是之后。
    /// </summary>
    [Required]
    [DefaultValue(nameof(InsertionPositionEnum.After))]
    [Display(
        Name = "插入位置",
        Description = "决定新提示词是插入到目标提示词之前还是之后。"
    )]
    [StringOptions([nameof(InsertionPositionEnum.Before), nameof(InsertionPositionEnum.After)], ["之前", "之后"])]
    public string InsertionPosition { get; init; } = nameof(InsertionPositionEnum.After);

    /// <summary>
    /// 指示最终操作是追加内容到上一个提示词 (true) 还是添加一个新的提示词项 (false)。
    /// </summary>
    [Required]
    [Display(
        Name = "追加模式",
        Description = "指示最终操作是追加内容到上一个提示词 (true) 还是添加一个新的提示词项 (false)。\n仅当本提示词和上一个提示词的角色、角色名相同时。"
    )]
    [DefaultValue(true)]
    public bool IsAppendMode { get; init; } = true;

    /// <summary>
    /// 提示词模板，支持 `[[占位符]]` 替换。
    /// 例如："你好，`[[playerName]]`！今天是`[[worldInfo]]`。"
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [DataType(DataType.MultilineText)]
    [Display(
        Name = "提示词模板",
        Description = "编写包含动态占位符（例如 `[[variable]]`）的文本模板。这个现在有点不方便，之后可能会改为其他的模板格式。",
        Prompt = "例如：'你好，`[[playerName]]`！今天是`[[worldInfo]]`。'"
    )]
    [DefaultValue("")]
    public string Template { get; init; } = "";


    /// <inheritdoc />
    protected override PromptGenerationRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) =>
        new(workflowRuntimeService, this);
}

file enum InsertionPositionEnum
{
    Before,
    After
}