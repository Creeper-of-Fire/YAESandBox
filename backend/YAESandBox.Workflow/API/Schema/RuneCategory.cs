using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using YAESandBox.Depend.Schema.SchemaProcessor.Abstract;

namespace YAESandBox.Workflow.API.Schema;

/// <summary>
/// 为符文配置指定一个分类，用于在前端 UI 中进行分组。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RuneCategoryAttribute(string category) : Attribute
{
    /// <summary>
    /// 分类路径，建议使用 "/" 作为层级分隔符。例如："文本处理/解析" 或 "核心功能"。
    /// </summary>
    public string Category { get; } = category;
}

/// <summary>
/// 处理 [RuneCategory] 特性，将其值添加到 JSON Schema 的 "x-rune-category" 扩展属性中。
/// </summary>
internal class RuneCategoryProcessor : YaeTypeAttributeProcessor<RuneCategoryAttribute>
{
    /// <inheritdoc />
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, RuneCategoryAttribute attribute)
    {
        // 将特性的 Category 属性值赋给 schema 的 "x-rune-category" 字段。
        // 我们使用 "x-" 前缀来遵循 JSON Schema 扩展的惯例。
        schema["x-rune-category"] = attribute.Category;
    }
}