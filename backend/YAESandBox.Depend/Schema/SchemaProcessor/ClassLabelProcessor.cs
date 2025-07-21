using YAESandBox.Depend.Schema.Attributes;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

internal class ClassLabelProcessor() : NormalActionProcessor(context =>
{
    var typeInfo = context.ContextualType;

    object[] attrs = typeInfo.GetCustomAttributes(true);
    if (attrs.OfType<ClassLabelAttribute>().FirstOrDefault() is not { } classLabelAttribute) return;
    
    context.Schema.ExtensionData ??= new Dictionary<string, object?>();
    context.Schema.ExtensionData["classLabel"] = classLabelAttribute.Label;
});