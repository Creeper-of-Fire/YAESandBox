using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 指示一个属性在前端应该被渲染成一个特定的自定义组件，
/// 而不是基于其 schema 类型默认的组件。
/// </summary>
/// <param name="widgetKey">在前端注册的自定义 Widget 的唯一键名。</param>
[AttributeUsage(AttributeTargets.Property)]
public class RenderWithCustomWidgetAttribute(string widgetKey) : Attribute
{
    /// <summary>
    /// 在前端注册的自定义 Widget 的唯一键名。
    /// 例如："preset-editor", "world-info-selector"
    /// </summary>
    public string WidgetKey { get; } = widgetKey;
}

/// <summary>
/// 处理 [RenderWithCustomWidget] 特性。
/// 它的唯一职责是在属性的 schema 中添加 'x-custom-renderer-property' 指令，
/// 以便前端能够查找并渲染对应的自定义组件。
/// 它会保留 schema 的原始类型（string, number, object 等）。
/// </summary>
internal class CustomWidgetRendererSchemaProcessor : YaePropertyAttributeProcessor<RenderWithCustomWidgetAttribute>
{
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema,
        RenderWithCustomWidgetAttribute attribute)
    {
        schema["x-custom-renderer-property"] = attribute.WidgetKey;
    }
}