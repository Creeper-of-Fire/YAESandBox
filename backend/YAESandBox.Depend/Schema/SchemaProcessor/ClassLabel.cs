using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using YAESandBox.Depend.Schema.SchemaProcessor.Abstract;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 给Class一个别名
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class ClassLabelAttribute(string label) : Attribute
{
    /// <summary>
    /// Class的标签
    /// </summary>
    public string Label { get; } = label;
}

internal class ClassLabelProcessor : YaeTypeAttributeProcessor<ClassLabelAttribute>
{
    /// <inheritdoc />
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, ClassLabelAttribute attribute) =>
        schema["x-classLabel"] = attribute.Label;
}