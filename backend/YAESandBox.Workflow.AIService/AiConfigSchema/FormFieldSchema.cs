namespace YAESandBox.Workflow.AIService.AiConfigSchema;

// DTO 用于传递给前端的字段元数据
public class FormFieldSchema
{
    public string Name { get; set; } = string.Empty; // 属性名 (C#)
    public string Label { get; set; } = string.Empty; // 显示名称
    public string? Description { get; set; } // 描述/提示
    public string? Placeholder { get; set; } // 输入框的 placeholder

    public SchemaDataType SchemaDataType { get; set; } = SchemaDataType.String;

    public bool IsReadOnly { get; set; }
    public bool IsRequired { get; set; }
    public object? DefaultValue { get; set; }
    public List<SelectOption>? Options { get; set; } // 用于枚举或下拉选择
    public ValidationRules? Validation { get; set; } // 校验规则
    public List<FormFieldSchema>? NestedSchema { get; set; } // 用于嵌套对象
    public FormFieldSchema? ArrayItemSchema { get; set; } // 用于数组，描述数组元素的Schema
    public int Order { get; set; } // 用于排序，如果需要
}

/// <summary>
/// 用于前端判断类型
/// </summary>
public enum SchemaDataType
{
    String,
    Number,
    Boolean,
    Enum,
    Object,
    Array,
    MultilineText,
    Password,
    Integer,
    DateTime,
    GUID,
    Unknown
}

public class SelectOption
{
    public object Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class ValidationRules
{
    public double? Min { get; set; } // for numbers
    public double? Max { get; set; } // for numbers
    public int? MinLength { get; set; } // for strings
    public int? MaxLength { get; set; } // for strings
    public string? Pattern { get; set; } // regex pattern
    public string? ErrorMessage { get; set; } // 通用错误信息（如果特性中定义了）
}