namespace YAESandBox.Depend.Schema.Attributes;

/// <summary>
/// 用于为属性提供字符串选项列表的特性，支持静态定义、动态获取以及可编辑下拉框行为。
/// 可用于前端生成下拉选择框、可编辑组合框等 UI 元素。
/// </summary>
/// <remarks>
/// 支持以下功能：
/// - 静态选项定义（Value/Label 对）
/// - 动态选项加载（通过 API 端点）
/// - 是否允许用户输入自定义值（combobox 行为）
/// </remarks>
/// <example>
/// 示例 1：静态定义 Value 和 Label 不同的选项
/// <code>
/// [StringOptions(
///     ("en", "English"),
///     ("zh", "中文"),
///     ("ja", "日本語"))]
/// public string Language { get; set; }
/// </code>
/// 
/// 示例 2：Value 和 Label 相同的简写形式
/// <code>
/// [StringOptions("Option1", "Option2", "Option3")]
/// public string Choices { get; set; }
/// </code>
/// 
/// 示例 3：使用动态端点加载选项并允许自定义输入
/// <code>
/// [StringOptions(optionsProviderEndpoint = "/api/options/roles", isEditableSelectOptions = true)]
/// public string Role { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public class StringOptionsAttribute() : Attribute
{
    /// <summary>
    /// 静态定义的选项列表。
    /// </summary>
    public (string Value, string Label)[] Options { get; } = [];

    /// <summary>
    /// 如果提供，表示该字段的选项可以从这个API端点动态获取。
    /// 前端可以调用此端点来刷新或获取选项列表。
    /// 动态获取的选项通常会与静态定义的 Options 合并或替换（行为由前端或帮助类决定）。
    /// </summary>
    public string? OptionsProviderEndpoint { get; set; }

    /// <summary>
    /// 是否允许用户输入不在建议选项列表中的自定义值 (可编辑下拉框)。
    /// 默认为 false，表示标准的固定选项下拉框。
    /// 当与 optionsProviderEndpoint 一起使用时，或即使 Options 为空但此值为 true，
    /// 通常暗示前端应渲染一个 combobox。
    /// </summary>
    public bool IsEditableSelectOptions { get; set; } = false;

    /// <summary>
    /// Value=Label时的构造函数
    /// </summary>
    /// <param name="options">字符串列表</param>
    public StringOptionsAttribute(params string[] options) : this(options.ToList().ConvertAll(option => (option, option)).ToArray()) { }

    /// <param name="options">选项列表</param>
    public StringOptionsAttribute(params (string Value, string Label)[] options) : this()
    {
        this.Options = options;
    }
}