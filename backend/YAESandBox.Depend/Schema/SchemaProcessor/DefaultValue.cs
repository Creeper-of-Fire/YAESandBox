using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using YAESandBox.Depend.Schema.SchemaProcessor.Abstract;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 处理 DefaultValueAttribute，将其值写入 JSON Schema 的 "default" 关键字。
/// </summary>
internal class DefaultValueProcessor : YaeGeneralAttributeProcessor<DefaultValueAttribute>
{
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, DefaultValueAttribute attribute)
    {
        if (attribute.Value is not null)
        {
            // 将 DefaultValueAttribute 的值转换为 JsonNode。
            // 简单地尝试将值转换为 JsonValue。
            // 对于复杂对象，这可能需要更复杂的序列化逻辑，
            // 但对于基本类型（字符串、数字、布尔等），JsonValue.Create 应该足够。
            try
            {
                schema["default"] = JsonValue.Create(attribute.Value);
            }
            catch (InvalidOperationException)
            {
                // 如果是复杂类型或无法直接转换为JsonValue，可以记录警告或忽略。
                // 为了示例简单，这里直接忽略。在实际应用中，你可能需要更健壮的处理。
                // 例如，如果DefaultValue是一个自定义对象，你可能需要序列化它。
                // For example, if DefaultValue is a custom object, you might need to serialize it.
                // context.Logger.LogWarning($"Could not set default value for {context.PropertyInfo?.Name ?? context.TypeInfo.Type.Name} because its type {attribute.Value.GetType().Name} is not directly supported by JsonValue.Create.");
            }
        }
    }
}