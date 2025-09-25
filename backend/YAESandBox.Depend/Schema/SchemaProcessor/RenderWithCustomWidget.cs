using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using YAESandBox.Depend.Schema.SchemaProcessor.Abstract;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 指示一个属性或类在前端应该被渲染成一个特定的自定义组件，
/// 而不是基于其 schema 类型默认的组件。
/// </summary>
/// <param name="widgetKey">在前端注册的自定义 Widget 的唯一键名。</param>
[AttributeUsage(AttributeTargets.Property| AttributeTargets.Class)]
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
/// 根据特性是附着在类还是属性上，为 Schema 添加 'x-custom-renderer-class' 或 'x-custom-renderer-property' 指令。
/// </summary>
internal class CustomWidgetRendererSchemaProcessor() : ComponentRendererProcessor<RenderWithCustomWidgetAttribute>(
    typeKey: "x-custom-renderer-class",         // 当应用于类时，使用这个key
    propertyKey: "x-custom-renderer-property",  // 当应用于属性时，使用这个key
    valueSelector: attr => attr.WidgetKey       // 从特性中提取组件名
);