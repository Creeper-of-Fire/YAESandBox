using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// DataType属性的处理器
/// </summary>
internal class DataTypeProcessor : YaePropertyAttributeProcessor<DataTypeAttribute>
{
    /// <inheritdoc />
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, DataTypeAttribute attribute)
    {
        schema["dataType"] = JsonNamingPolicy.CamelCase.ConvertName(attribute.DataType.ToString());
    }
}