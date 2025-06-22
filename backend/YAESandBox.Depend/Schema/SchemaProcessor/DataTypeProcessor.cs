using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using YAESandBox.Depend.Schema.Attributes;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// DataType属性的处理器
/// </summary>
internal class DataTypeProcessor() : NormalAttributeProcessor<DataTypeAttribute>((extensionData, attribute) =>
{
    extensionData["dataType"] = JsonNamingPolicy.CamelCase.ConvertName(attribute.DataType.ToString());
});

internal class ClassLabelProcessor() : NormalActionProcessor(context =>
{
    var typeInfo = context.ContextualType;

    object[] attrs = typeInfo.GetCustomAttributes(true);
    if (attrs.OfType<ClassLabelAttribute>().FirstOrDefault() is not { } classLabelAttribute) return;
    
    context.Schema.ExtensionData ??= new Dictionary<string, object?>();
    context.Schema.ExtensionData["classLabel"] = classLabelAttribute.Label;
});