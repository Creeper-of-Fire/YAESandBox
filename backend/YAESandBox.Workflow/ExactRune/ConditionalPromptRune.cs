using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Jint;
using Jint.Native;
using Jint.Runtime;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Runtime.Processor;
using static YAESandBox.Workflow.Runtime.Processor.TuumProcessor;

namespace YAESandBox.Workflow.ExactRune;

/// <summary>
/// “条件提示词”符文的运行时处理器。
/// 它在 PromptGenerationRuneProcessor 的基础上增加了条件判断逻辑。
/// </summary>
internal class ConditionalPromptRuneProcessor(ConditionalPromptRuneConfig config, ICreatingContext creatingContext)
    // 继承自 PromptGenerationRuneProcessor 以复用其所有功能
    : PromptGenerationRuneProcessor(config, creatingContext)
{
    // 强制类型转换，以便访问 Condition 属性
    private new ConditionalPromptRuneConfig Config => (ConditionalPromptRuneConfig)base.Config;

    /// <summary>
    /// 覆盖基类的 DebugDto，以添加条件执行相关的信息。
    /// </summary>
    public override ConditionalPromptRuneProcessorDebugDto DebugDto { get; } = new()
    {
        // 初始化基类部分的调试信息
        OriginalTemplate = config.Template,
        ConfiguredRole = PromptRoleTypeExtension.ToPromptRoleType(config.RoleType),
        ConfiguredPromptName = config.PromptNameInAiModel,
        // 初始化本类新增的调试信息
        Condition = config.Condition,
    };

    /// <summary>
    /// 覆盖基类的 ExecuteAsync 方法，以首先执行条件判断。
    /// </summary>
    public override async Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent,
        CancellationToken cancellationToken = default)
    {
        bool shouldExecute = false;
        string? evalError = null;

        // 1. 如果条件为空或仅有空白，则默认为 true
        if (string.IsNullOrWhiteSpace(this.Config.Condition))
        {
            shouldExecute = true;
            this.DebugDto.WasConditionMet = true;
            this.DebugDto.EvaluationResult = "条件为空，默认为真。";
        }
        else
        {
            // 2. 准备 Jint 引擎
            var engine = new Engine(options =>
            {
                // ** 安全沙盒配置 **
                options.TimeoutInterval(TimeSpan.FromSeconds(2)); // 防止无限循环
                options.Constraints.MaxRecursionDepth = 10;
                options.LimitMemory(4 * 1024 * 1024); // 4MB 内存限制
                options.MaxStatements(1000); // 最多执行1000条语句
                options.CancellationToken(cancellationToken);
                options.AllowClr(); // 【重要】禁止访问.NET CLR，确保沙盒安全
            });

            try
            {
                // 3. 将所有消耗的变量注入到 Jint 作用域
                foreach (var consumed in this.Config.GetConsumedSpec())
                {
                    // GetTuumVar<object> 可以获取任何类型的变量
                    object? varValue = tuumProcessorContent.GetTuumVar<object>(consumed.Name);
                    engine.SetValue(consumed.Name, varValue);
                }

                // 4. 执行条件表达式
                JsValue result = engine.Evaluate(this.Config.Condition);
                this.DebugDto.EvaluationResult = result.ToString();

                // 5. 检查结果
                if (result.IsBoolean())
                {
                    shouldExecute = result.AsBoolean();
                    this.DebugDto.WasConditionMet = shouldExecute;
                }
                else
                {
                    evalError = "条件表达式的返回结果不是一个布尔值 (true/false)。";
                }
            }
            catch (Exception ex) when (ex is JintException or JavaScriptException)
            {
                evalError = $"Jint 脚本执行失败。 {ex.ToFormattedString()}";
            }
            catch (Exception ex)
            {
                evalError = $"未预料的错误: {ex.ToFormattedString()}";
            }
        }

        // 6. 如果有错误，记录并返回失败
        if (evalError != null)
        {
            this.DebugDto.EvaluationError = evalError;
            return Result.Fail($"条件提示词 '{this.Config.Name}' 的条件执行失败: {evalError}");
        }

        // 7. 如果条件满足，则调用基类的执行方法来完成实际的提示词生成
        if (shouldExecute)
        {
            var baseResult = await base.ExecuteAsync(tuumProcessorContent, cancellationToken);
            // 将基类执行后的 DebugDto 存入我们自己的 Dto 中
            this.DebugDto.BasePromptDebugInfo = base.DebugDto;
            return baseResult;
        }

        // 8. 如果条件不满足，则什么都不做，直接返回成功
        return Result.Ok();
    }


    /// <summary>
    /// 条件提示词符文的专属调试 DTO。
    /// </summary>
    public new record ConditionalPromptRuneProcessorDebugDto : PromptGenerationRuneProcessorDebugDto
    {
        // --- 条件判断相关 ---
        [Display(Name = "执行条件")] public string Condition { get; set; } = string.Empty;

        [Display(Name = "条件是否满足")] public bool? WasConditionMet { get; set; }

        [Display(Name = "表达式计算结果")] public string? EvaluationResult { get; set; }

        [Display(Name = "计算错误")] public string? EvaluationError { get; set; }

        // --- 嵌套的基础提示词生成调试信息 ---
        [Display(Name = "提示词生成详情 (当条件满足时)")] public PromptGenerationRuneProcessorDebugDto? BasePromptDebugInfo { get; set; }
    }
}

/// <summary>
/// “条件提示词”符文的配置。
/// 它继承了 PromptGenerationRuneConfig 的所有功能，并增加了一个执行条件。
/// </summary>
[ClassLabel("✍️条件提示词")]
internal record ConditionalPromptRuneConfig : PromptGenerationRuneConfig
{
    /// <summary>
    /// 一个 JavaScript 布尔表达式。只有当此表达式的计算结果为 true 时，此提示词才会被生成。
    /// 表达式可以访问此符文“消耗”的所有变量。
    /// </summary>
    [DataType(DataType.MultilineText)]
    [RenderWithMonacoEditor("javascript")]
    [Display(
        Name = "执行条件",
        Description = "编写一个JavaScript布尔表达式。只有当结果为 true 时，才会生成此提示词。例如：player.level > 10 && has_quest == true"
    )]
    [DefaultValue("true")] // 默认值为 true，使其在不配置时等同于一个普通的提示词生成符文
    public string Condition { get; init; } = "true";

    /// <inheritdoc />
    protected override PromptGenerationRuneProcessor ToCurrentRune(ICreatingContext creatingContext) =>
        new ConditionalPromptRuneProcessor(this, creatingContext);

    // GetConsumedSpec 和 GetProducedSpec 会被基类 PromptGenerationRuneConfig 自动处理，
    // Jint 的注入逻辑会智能地使用 GetConsumedSpec 的结果，无需我们重写。
}