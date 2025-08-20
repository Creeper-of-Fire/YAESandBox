using System.Text.Json.Nodes;
using System.Text.Json.Schema;

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
/// 2. **移除所有内部属性定义（properties, $ref 等），使其成为不透明对象。**
/// 3. 添加 'x-custom-renderer-property' 指令，以便前端查找组件。
/// </summary>
internal class CustomObjectWidgetRendererSchemaProcessor : YaePropertyAttributeProcessor<RenderAsCustomObjectWidgetAttribute>
{
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, RenderAsCustomObjectWidgetAttribute attribute)
    {
        // 步骤 1: 确保类型是 'object'
        // Exporter 默认会为复杂类型生成 "type": "object"，但我们最好还是确保一下
        schema["type"] = "object";

        // 步骤 2: 使其成为不透明对象。
        // 这是关键！我们不再需要清理 NJsonSchema 的各种特定属性，
        // 而是直接移除通用的、可能导致前端展开的 JSON Schema 关键字。
        schema.Remove("properties");
        schema.Remove("patternProperties");
        schema.Remove("allOf");
        schema.Remove("anyOf");
        schema.Remove("oneOf");
        schema.Remove("$ref"); // 特别重要！移除可能存在的引用，强制内联为一个不透明对象

        // 你甚至可以更激进一点，创建一个全新的 JsonObject 来替换它，
        // 确保绝对干净，但这通常不是必需的。
        // 例如：
        // schema.Clear();
        // schema["type"] = "object";

        // 步骤 3: 注入前端指令
        schema["x-custom-renderer-property"] = attribute.WidgetKey;
    }
}