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

internal class ClassLabelProcessor() : NormalActionProcessor(context =>
{
    var typeInfo = context.ContextualType;

    object[] attrs = typeInfo.GetCustomAttributes(true);
    if (attrs.OfType<ClassLabelAttribute>().FirstOrDefault() is not { } classLabelAttribute) return;

    context.Schema.ExtensionData ??= new Dictionary<string, object?>();
    context.Schema.ExtensionData["x-classLabel"] = classLabelAttribute.Label;
});