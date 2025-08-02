using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Rune.ExactRune.PromptGenerationRuneProcessor;
using static YAESandBox.Workflow.Step.StepProcessor;

namespace YAESandBox.Workflow.Rune.ExactRune;

/// <summary>
/// 提示词生成符文处理器。
/// 根据配置的模板和上下文数据，生成一个RoledPromptDto。
/// </summary>
/// <param name="workflowRuntimeService"><see cref="WorkflowRuntimeService"/></param>
/// <param name="config">符文配置。</param>
internal partial class PromptGenerationRuneProcessor(
    WorkflowRuntimeService workflowRuntimeService,
    PromptGenerationRuneConfig config)
    : IWithDebugDto<PromptGenerationRuneProcessorDebugDto>, INormalRune
{
    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;
    private PromptGenerationRuneConfig Config { get; init; } = config;

    /// <inheritdoc />
    public PromptGenerationRuneProcessorDebugDto DebugDto { get; init; } = new()
    {
        OriginalTemplate = config.Template,
        ConfiguredRole = PromptRoleTypeExtension.ToPromptRoleType(config.RoleType),
        ConfiguredPromptName = config.PromptNameInAiModel,
    };

    /// <summary>
    /// 启动步骤流程
    /// </summary>
    /// <param name="stepProcessorContent">步骤执行的上下文内容。</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<Result> ExecuteAsync(StepProcessorContent stepProcessorContent, CancellationToken cancellationToken = default)
    {
        string substitutedContent = this.SubstitutePlaceholdersAsync(this.Config.Template, stepProcessorContent);
        this.DebugDto.FinalPromptContent = substitutedContent;

        var prompt = new RoledPromptDto
        {
            Role = PromptRoleTypeExtension.ToPromptRoleType(this.Config.RoleType),
            Content = substitutedContent,
            Name = this.Config.PromptNameInAiModel ?? string.Empty
        };

        stepProcessorContent.Prompts.Add(prompt);

        this.DebugDto.GeneratedPrompt = prompt;
        return Task.FromResult(Result.Ok());
    }


    private string SubstitutePlaceholdersAsync(
        string template,
        StepProcessorContent stepContent)
    {
        var resolvedValues = new Dictionary<string, string?>();

        var uniquePlaceholderNames = PromptGenerationRuneConfig.PlaceholderRegex().Matches(template)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase) // 占位符名称不区分大小写
            .ToList();

        foreach (string placeholderName in uniquePlaceholderNames)
        {
            string? value = null;
            bool found = false;

            // 尝试从 StepVariable (dynamic, 假设其内部是 IDictionary<string, object>)
            try
            {
                if (stepContent.StepVariable is IDictionary<string, object> stepInputDict &&
                    stepInputDict.TryGetValue(placeholderName, out object? stepValObj))
                {
                    value = stepValObj.ToString();
                    found = true;
                }
            }
            catch (Exception ex)
            {
                // 记录尝试从StepInput获取时的潜在错误到调试信息
                this.DebugDto.AddResolutionAttemptLog($"尝试从 StepVariable 获取 '{placeholderName}' 失败: {ex.Message}");
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
        /// 经过占位符替换后的最终提示词内容。
        /// </summary>
        public string FinalPromptContent { get; internal set; } = string.Empty;

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
    internal override List<string> GetConsumedVariables() => PlaceholderRegex().Matches(this.Template)
        .Select(m => m.Groups[1].Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    
    internal override List<string> GetProducedVariables() => [AiRuneConfig.PromptsName];
}

/// <summary>
/// 提示词生成符文的配置。
/// </summary>
[InFrontOf(typeof(AiRuneConfig))]
[ClassLabel("✍️提示词")]
internal partial record PromptGenerationRuneConfig : AbstractRuneConfig<PromptGenerationRuneProcessor>
{
    /// <summary>
    /// 生成的提示词的角色类型 (System, User, Assistant)。
    /// </summary>
    [Required]
    [Display(
        Name = "提示词角色类型",
        Description = "选择此提示词在对话历史中扮演的角色。"
    )]
    [StringOptions(["System", "User", "Assistant"], ["系统", "用户", "AI助手"])]
    public required string RoleType { get; init; }

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
    /// 提示词模板，支持 `[[占位符]]` 替换。
    /// 例如："你好，`[[playerName]]`！今天是`[[worldInfo]]`。"
    /// </summary>
    [Required]
    [DataType(DataType.MultilineText)]
    [Display(
        Name = "提示词模板",
        Description = "编写包含动态占位符（例如 `[[variable]]`）的文本模板。这个现在有点不方便，之后可能会改为其他的模板格式。",
        Prompt = "例如：'你好，`[[playerName]]`！今天是`[[worldInfo]]`。'"
    )]
    [DefaultValue("")]
    public required string Template { get; init; } = "";


    /// <inheritdoc />
    protected override PromptGenerationRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) =>
        new(workflowRuntimeService, this);
}