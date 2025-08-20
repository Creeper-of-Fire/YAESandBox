using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 有内联的属性组
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class InlineGroupAttribute(string groupName) : Attribute
{
    /// <summary>
    /// InlineGroup 的名称
    /// </summary>
    public string GroupName { get; } = groupName;
}

internal class InlineGroupProcessor : YaePropertyAttributeProcessor<InlineGroupAttribute>
{
    /// <inheritdoc />
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, InlineGroupAttribute attribute) =>
        schema["ui:inlineGroup"] = attribute.GroupName;
}