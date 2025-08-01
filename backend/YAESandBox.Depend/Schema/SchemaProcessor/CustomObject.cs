using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 指示一个复杂的对象属性在前端应该被渲染成一个单一的、不透明的自定义组件，
/// 而不是展开其内部属性。
/// </summary>
/// <param name="widgetKey">在前端注册的自定义 Widget 的唯一键名。</param>
[AttributeUsage(AttributeTargets.Property)]
public class RenderAsCustomObjectWidgetAttribute(string widgetKey) : Attribute
{
    /// <summary>
    /// 
    /// </summary>
    public string WidgetKey { get; } = widgetKey;
}

/// <summary>
/// 处理 [RenderAsCustomObjectWidget] 特性。
/// 1. 将 schema 的类型强制设为 'object'。
/// 2. **移除所有内部属性定义（properties, oneOf, allOf 等），使其成为不透明对象。**
/// 3. 添加 'x-custom-renderer' 指令，以便前端查找组件。
/// </summary>
internal class CustomObjectWidgetRendererSchemaProcessor : ISchemaProcessor
{
    public void Process(SchemaProcessorContext context)
    {
        var attribute = context.ContextualType.GetContextAttribute<RenderAsCustomObjectWidgetAttribute>(true);
        if (attribute == null) return;

        var schema = context.Schema;

        // 步骤 1 & 2: 使其成为不透明对象
        schema.Type = JsonObjectType.Object;
        schema.Properties.Clear();
        schema.PatternProperties.Clear();
        schema.AllOf.Clear();
        schema.AnyOf.Clear();
        schema.OneOf.Clear();
        // 清理其他可能导致展开的元数据
        schema.ExtensionData ??= new Dictionary<string, object?>();

        // 步骤 3: 注入前端指令
        schema.ExtensionData["x-custom-renderer"] = attribute.WidgetKey;
    }
}