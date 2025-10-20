using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

namespace YAESandBox.Workflow.ExactRune;

/// <summary>
/// 提示词生成符文处理器。
/// 根据配置的模板和上下文数据，生成一个RoledPromptDto。
/// </summary>
/// <param name="creatingContext"></param>
/// <param name="config">符文配置。</param>
internal class PromptGenerationRuneProcessor(PromptGenerationRuneConfig config, ICreatingContext creatingContext)
    : NormalRuneProcessor<PromptGenerationRuneConfig, PromptGenerationRuneProcessor.PromptGenerationRuneProcessorDebugDto>(config,
        creatingContext)
{
    /// <inheritdoc />
    public override PromptGenerationRuneProcessorDebugDto DebugDto { get; } = new()
    {
        OriginalTemplate = config.Template,
        ConfiguredRole = PromptRoleTypeExtension.ToPromptRoleType(config.RoleType),
        ConfiguredPromptName = config.PromptNameInAiModel,
    };

    /// <summary>
    /// 启动枢机流程
    /// </summary>
    /// <inheritdoc />
    public override Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        string substitutedContent = this.SubstitutePlaceholders(this.Config.Template, tuumProcessorContent);
        this.DebugDto.FinalPromptContent = substitutedContent;

        var newPrompt = new RoledPromptDto
        {
            Role = PromptRoleTypeExtension.ToPromptRoleType(this.Config.RoleType),
            Content = substitutedContent,
            Name = this.Config.PromptNameInAiModel ?? string.Empty
        };
        this.DebugDto.GeneratedPrompt = newPrompt;

        var prompts =
            (tuumProcessorContent.GetTuumVar<ImmutableList<RoledPromptDto>>(PromptGenerationRuneConfig.PromptsName) ?? []).ToList();

        if (prompts.Count == 0)
        {
            // 如果列表为空，直接添加新提示词
            this.DebugDto.FinalAction = "在一个空列表中添加第一个提示词。";
            prompts.Add(newPrompt);
            tuumProcessorContent.SetTuumVar(PromptGenerationRuneConfig.PromptsName, prompts);

            return Result.Ok().AsCompletedTask();
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

        tuumProcessorContent.SetTuumVar(PromptGenerationRuneConfig.PromptsName, prompts);

        return Result.Ok().AsCompletedTask();
    }


    /// <summary>
    /// 使用 Tuum 上下文中的变量替换模板中的占位符，支持点符号访问。
    /// </summary>
    private string SubstitutePlaceholders(string template, TuumProcessorContent tuumContent)
    {
        return PromptGenerationRuneConfig.PlaceholderRegex().Replace(template, match =>
        {
            // 例如: 'player.name'
            string path = match.Groups[1].Value;

            // 使用 TuumContent 提供的路径解析方法
            if (tuumContent.TryGetTuumVarByPath<object>(path, out object? value))
            {
                string stringValue = value.ToString() ?? string.Empty;
                this.DebugDto.ResolvedPlaceholdersWithValue[path] = stringValue;
                return stringValue;
            }

            // 未找到或值为 null
            this.DebugDto.UnresolvedPlaceholders.Add(path);
            this.DebugDto.AddResolutionAttemptLog($"占位符 '{{{{{path}}}}}' 未找到或其值为 null，将替换为空字符串。");
            return string.Empty;
        });
    }


    /// <summary>
    /// 提示词生成符文处理器的调试数据传输对象。
    /// </summary>
    internal record PromptGenerationRuneProcessorDebugDto : IRuneProcessorDebugDto
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
        /// 值: 替换的文本
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
    // 使用 lookahead 和 lookbehind 来确保我们只匹配 {{}} 包裹的内容，并且不能处理嵌套 {{}} 的情况
    // TODO 实现嵌套等操作
    [GeneratedRegex(@"\{\{([^}]+?)\}\}")]
    internal static partial Regex PlaceholderRegex();

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec()
    {
        // 用于存储根变量及其推断出的结构
        var rootSpecs = new Dictionary<string, VarSpecDef>();

        var allPlaceholders = PlaceholderRegex().Matches(this.Template)
            .Select(m => m.Groups[1].Value.Trim());

        foreach (string path in allPlaceholders)
        {
            string[] parts = path.Split('.');
            if (parts.Length == 0) continue;

            string rootVarName = parts[0];

            // --- 顶层变量处理 ---
            if (parts.Length == 1)
            {
                // 如果是顶层变量 (e.g., {{game_mode}}), 且尚未定义，则将其定义为 Any
                if (!rootSpecs.ContainsKey(rootVarName))
                {
                    rootSpecs[rootVarName] = CoreVarDefs.Any with
                    {
                        Description = "可被ToString的任意类型。"
                    };
                }

                // 如果已存在且是 Record, 则不作处理，因为 Record 可以被 ToString()
                continue;
            }

            // --- 嵌套变量处理 (核心逻辑) ---

            // 确保根变量是 Record 类型
            if (!rootSpecs.TryGetValue(rootVarName, out var currentDef) || currentDef is not RecordVarSpecDef)
            {
                currentDef = CoreVarDefs.RecordStringAny with
                {
                    Description = $"根据模板为变量'{rootVarName}'推断出的数据结构。",
                    Properties = new Dictionary<string, VarSpecDef>()
                };
                rootSpecs[rootVarName] = currentDef;
            }

            var currentRecord = (RecordVarSpecDef)currentDef;

            // 递归地构建/遍历属性路径
            for (int i = 1; i < parts.Length; i++)
            {
                string propName = parts[i];

                // 如果是路径的最后一部分 (叶子节点)
                if (i == parts.Length - 1)
                {
                    if (!currentRecord.Properties.ContainsKey(propName))
                    {
                        // 叶子节点默认为 Any，因为它可以是任何能被 ToString() 的类型
                        currentRecord.Properties[propName] = CoreVarDefs.Any with
                        {
                            Description = "可被ToString的任意类型。"
                        };
                    }

                    break;
                }

                // 如果是路径的中间部分 (非叶子节点)
                if (!currentRecord.Properties.TryGetValue(propName, out var nextDef) || nextDef is not RecordVarSpecDef)
                {
                    nextDef = CoreVarDefs.RecordStringAny with
                    {
                        Description = $"为'{propName}'推断出的嵌套数据结构。",
                        Properties = new Dictionary<string, VarSpecDef>()
                    };
                    currentRecord.Properties[propName] = nextDef;
                }

                currentRecord = (RecordVarSpecDef)nextDef;
            }
        }

        // 将推断出的结构转换为 ConsumedSpec 列表
        var finalSpecs = rootSpecs.Select(kvp => new ConsumedSpec(kvp.Key, kvp.Value)).ToList();

        // 添加对 Prompts 列表本身的消费
        finalSpecs.Add(new ConsumedSpec(PromptsName, CoreVarDefs.PromptList) { IsOptional = true });

        return finalSpecs;
    }

    public override List<ProducedSpec> GetProducedSpec() => [new(PromptsName, CoreVarDefs.PromptList)];
}

/// <summary>
/// 提示词生成符文的配置。
/// </summary>
[InFrontOf(typeof(AiRuneConfig))]
[ClassLabel("✍️提示词")]
[RuneCategory("提示词处理")]
internal partial record PromptGenerationRuneConfig : AbstractRuneConfig<PromptGenerationRuneProcessor>
{
    private const string RoleGroupName = "提示词角色";
    private const string InsertGroupName = "插入";

    /// <summary>
    /// 使用的提示词列表变量的名称（若存在则在列表中添加，若不存在则创建）。
    /// </summary>
    [JsonIgnore]
    public static string PromptsName => AiRuneConfig.PromptsDefaultName;

    /// <summary>
    /// 生成的提示词的角色类型 (System, User, Assistant)。
    /// </summary>
    [Required]
    [InlineGroup(RoleGroupName)]
    [Display(
        Name = "角色类型",
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
        Name = "角色名",
        Description = "为提示词角色指定一个具体名称，可以让某些模型更好的区分不同的用户或助手，对于部分高级模型有用。",
        Prompt = "例如：'DeepSeek' 或 'はちみ'"
    )]
    [InlineGroup(RoleGroupName)]
    public string? PromptNameInAiModel { get; init; }

    /// <summary>
    /// 提示词插入的深度。0代表在列表末尾操作，1代表在倒数第二个提示词附近操作，以此类推。如果深度超出范围，则定位到列表的第一个提示词。
    /// </summary>
    [Required]
    [DefaultValue(0)]
    [InlineGroup(InsertGroupName)]
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
    [InlineGroup(InsertGroupName)]
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
    [InlineGroup(InsertGroupName)]
    [DefaultValue(true)]
    public bool IsAppendMode { get; init; } = true;

    /// <summary>
    /// 提示词模板，支持 `{{占位符}}` 和 `{{对象.属性}}` 替换。
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [DataType(DataType.MultilineText)]
    [Display(
        Name = "提示词模板",
        Description = "编写包含动态占位符（例如 `{{variable}}` 或 `{{player.name}}`）的文本模板。",
        Prompt = "例如：'你好，{{player.name}}！你的等级是{{player.level}}。'"
    )]
    [DefaultValue("")]
    public string Template { get; init; } = "";


    /// <inheritdoc />
    protected override PromptGenerationRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);
}

file enum InsertionPositionEnum
{
    Before,
    After
}