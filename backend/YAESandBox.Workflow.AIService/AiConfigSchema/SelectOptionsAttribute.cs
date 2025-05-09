namespace YAESandBox.Workflow.AIService.AiConfigSchema;

[AttributeUsage(AttributeTargets.Property)]
public class SelectOptionsAttribute(params SelectOption[] options) : Attribute
{
    /// <summary>
    /// 静态定义的选项列表。
    /// </summary>
    public SelectOption[] Options { get; } = options;

    /// <summary>
    /// 如果提供，表示该字段的选项可以从这个API端点动态获取。
    /// 前端可以调用此端点来刷新或获取选项列表。
    /// 动态获取的选项通常会与静态定义的 Options 合并或替换（行为由前端或帮助类决定）。
    /// </summary>
    public string? OptionsProviderEndpoint { get; set; }

    /// <summary>
    /// 是否允许用户输入不在建议选项列表中的自定义值 (可编辑下拉框)。
    /// 默认为 false，表示标准的固定选项下拉框。
    /// 当与 OptionsProviderEndpoint 一起使用时，或即使 Options 为空但此值为 true，
    /// 通常暗示前端应渲染一个 combobox。
    /// </summary>
    public bool IsEditableSelectOptions { get; set; } = false;

    /// <summary>
    /// 空构造函数，主要用于当只希望指定 OptionsProviderEndpoint 和/或 IsCreatable，
    /// 而不提供任何初始静态选项时。
    /// </summary>
    public SelectOptionsAttribute() : this(Array.Empty<SelectOption>()) { }

    /// <summary>
    /// 使用字符串数组构造，每个字符串同时作为 Value 和 Label。
    /// </summary>
    /// <param name="options">选项字符串数组。</param>
    public SelectOptionsAttribute(params string[] options) : this(options.ToList()
        .ConvertAll(str => new SelectOption { Value = str, Label = str }).ToArray()) { }
}