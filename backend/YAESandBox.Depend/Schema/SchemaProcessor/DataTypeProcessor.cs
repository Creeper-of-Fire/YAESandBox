using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// DataType属性的处理器
/// </summary>
internal class DataTypeProcessor() : NormalAttributeProcessor<DataTypeAttribute>((extensionData, attribute) =>
{
    extensionData["dataType"] = JsonNamingPolicy.CamelCase.ConvertName(attribute.DataType.ToString());
});