// FormFieldSchema.cs

using System.ComponentModel.DataAnnotations;

namespace YAESandBox.Workflow.AIService.AiConfigSchema;

/// <summary>
/// 用于描述表单字段的元数据，传递给前端以动态生成表单。
/// </summary>
public class FormFieldSchema
{
    /// <summary>
    /// 字段的编程名称（通常是C#属性名）。
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 字段在UI上显示的标签文本。
    /// </summary>
    [Required]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 对字段的额外描述或提示信息，显示在标签下方或作为tooltip。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 输入框的占位提示文本 (placeholder)。
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// 字段的数据类型，用于前端决定渲染何种输入控件。
    /// </summary>
    [Required]
    public SchemaDataType SchemaDataType { get; set; } = SchemaDataType.String;

    /// <summary>
    /// 字段是否为只读。
    /// </summary>
    [Required]
    public bool IsReadOnly { get; set; } = false;

    /// <summary>
    /// 字段是否为必填。
    /// </summary>
    [Required]
    public bool IsRequired { get; set; } = false;

    /// <summary>
    /// 字段的默认值。
    /// </summary>
    public object? DefaultValue { get; set; } // 注意：DefaultValue 的实际类型应与 SchemaDataType 匹配

    /// <summary>
    /// 用于选择类型（如枚举、下拉列表）的选项列表。
    /// 如果 <see cref="IsEditableSelectOptions"/> 为 true，这些是建议选项，用户仍可输入自定义值。
    /// 默认先选择第一个，如果<see cref="DefaultValue"/>不为空，尝试从<see cref="SelectOption.Value"/>和<see cref="SelectOption.Label"/>属性中进行匹配
    /// </summary>
    public List<SelectOption>? Options { get; set; }

    /// <summary>
    /// 如果为 true，并且 Options 不为空，表示这是一个可编辑的下拉框 (combobox)。
    /// 用户可以选择建议的 Options，也可以输入不在列表中的自定义值。
    /// 例如，如果 SchemaDataType 是 String，且 Options 为 null 或空，但此值为 true，
    /// 暗示前端可能需要一个普通的文本输入，但可能带有某种自动完成或建议机制（如果 OptionsProviderEndpoint 指定）。
    /// </summary>
    [Required]
    public bool IsEditableSelectOptions { get; set; } = false;

    /// <summary>
    /// 如果提供，表示该字段的选项可以从这个API端点动态获取。
    /// 前端可以调用此端点（通常是GET请求）来刷新或获取选项列表。
    /// 端点应返回 SelectOption[] 或类似结构。
    /// 例如："/api/ai-models/doubao/available-models"
    /// </summary>
    public string? OptionsProviderEndpoint { get; set; }

    /// <summary>
    /// 字段的校验规则。
    /// </summary>
    public ValidationRules? Validation { get; set; }

    /// <summary>
    /// 如果 SchemaDataType 是 Object，此属性包含该嵌套对象的字段定义。
    /// </summary>
    public List<FormFieldSchema>? NestedSchema { get; set; }

    /// <summary>
    /// 如果 SchemaDataType 是 Array，此属性描述数组中每个元素的Schema。
    /// </summary>
    public FormFieldSchema? ArrayItemSchema { get; set; }

    /// <summary>
    /// 如果 SchemaDataType 是 Dictionary，此属性描述字典键的信息。
    /// </summary>
    public DictionaryKeyInfo? KeyInfo { get; set; }

    /// <summary>
    /// 如果 SchemaDataType 是 Dictionary，此属性描述字典中每个值的Schema。
    /// </summary>
    public FormFieldSchema? DictionaryValueSchema { get; set; }

    /// <summary>
    /// 字段在表单中的显示顺序，值越小越靠前。
    /// </summary>
    [Required]
    public int Order { get; set; }
}

/// <summary>
/// 定义了Schema字段支持的主要数据类型，供前端进行UI渲染决策。
/// String, // 普通字符串
/// Number, // 包含整数和浮点数
/// Boolean, // 布尔值 (true/false)
/// Enum, // 枚举类型，通常配合 Options 使用
/// Object, // 嵌套的复杂对象，其结构由 NestedSchema 定义
/// Array, // 数组/列表，其元素结构由 ArrayItemSchema 定义
/// MultilineText, // 多行文本输入 (textarea)
/// Password, // 密码输入框
/// Integer, // 专指整数
/// DateTime, // 日期或日期时间
/// GUID, // GUID 全局唯一标识符
/// Dictionary, // 字典/映射类型，键信息由 KeyInfo 定义，值结构由 DictionaryValueSchema 定义
/// Unknown // 未知或不支持的类型
/// </summary>
public enum SchemaDataType
{
    String, // 普通字符串
    Number, // 包含整数和浮点数
    Boolean, // 布尔值 (true/false)
    Enum, // 枚举类型，通常配合 Options 使用
    Object, // 嵌套的复杂对象，其结构由 NestedSchema 定义
    Array, // 数组/列表，其元素结构由 ArrayItemSchema 定义
    MultilineText, // 多行文本输入 (textarea)
    Password, // 密码输入框
    Integer, // 专指整数
    DateTime, // 日期或日期时间
    GUID, // GUID 全局唯一标识符
    Dictionary, // 字典/映射类型，键信息由 KeyInfo 定义，值结构由 DictionaryValueSchema 定义
    Unknown // 未知或不支持的类型
}

/// <summary>
/// 代表一个选择项，用于下拉列表或单选/复选按钮组。
/// </summary>
public class SelectOption
{
    /// <summary>
    /// 选项的实际值。
    /// </summary>
    [Required]
    public object Value { get; init; } = string.Empty;

    /// <summary>
    /// 选项在UI上显示的文本。
    /// </summary>
    [Required]
    public string Label { get; init; } = string.Empty;
}

/// <summary>
/// 包含字段的校验规则。
/// </summary>
public class ValidationRules
{
    /// <summary>
    /// 对于数字类型，允许的最小值。
    /// </summary>
    public double? Min { get; set; }

    /// <summary>
    /// 对于数字类型，允许的最大值。
    /// </summary>
    public double? Max { get; set; }

    /// <summary>
    /// 对于数字类型，调整的步长。
    /// </summary>
    /// <returns></returns>
    public double? Step { get; set; }

    /// <summary>
    /// 对于字符串类型，允许的最小长度。
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// 对于字符串类型，允许的最大长度。
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// 正则表达式模式，用于校验输入。
    /// 也可用于特殊标记，如 "url"，由前端特定处理。
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// 当校验失败时显示的通用错误信息。
    /// 如果多个校验特性都提供了错误信息，它们可能会被合并。
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 描述字典类型字段中“键”的相关信息。
/// </summary>
public class DictionaryKeyInfo
{
    /// <summary>
    /// 字典键的基础数据类型 (如 String, Integer, Enum, Guid)。
    /// </summary>
    [Required]
    public SchemaDataType KeyType { get; set; }

    /// <summary>
    /// 如果 KeyType 是 Enum，这里提供枚举的选项列表。
    /// </summary>
    public List<SelectOption>? EnumOptions { get; set; }

    /// <summary>
    /// 原始C#键类型名称，用于调试或特定场景。
    /// </summary>
    public string? RawKeyTypeName { get; set; }
}