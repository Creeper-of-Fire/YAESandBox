using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using YAESandBox.Depend.Schema.SchemaProcessor.Abstract;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 为类或接口提供显示名称（标签）和可选的图标标识。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class ClassLabelAttribute(string label) : Attribute
{
    /// <summary>
    /// 类的显示标签（名称）。
    /// </summary>
    public string Label { get; } = label;

    /// <summary>
    /// (可选) 用于在UI中表示该类的图标。
    /// 可能是emoji、svg、网址等。
    /// </summary>
    public string? Icon { get; set; }
}

internal class ClassLabelProcessor : YaeTypeAttributeProcessor<ClassLabelAttribute>
{
    /// <inheritdoc />
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, ClassLabelAttribute attribute)
    {
        // 将 Label 添加到 schema
        schema["x-classLabel"] = attribute.Label;

        // 如果 Icon 属性被设置了，并且不是空字符串，也添加到 schema
        if (!string.IsNullOrEmpty(attribute.Icon))
        {
            schema["x-classLabel-icon"] = attribute.Icon;
        }
    }
}