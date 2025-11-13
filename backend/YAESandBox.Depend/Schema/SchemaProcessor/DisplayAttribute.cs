using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using YAESandBox.Depend.Schema.SchemaProcessor.Abstract;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 处理 [DisplayAttribute] 为 Schema 添加 title, description, placeholder, 和排序信息。
/// </summary>
internal class DisplayAttributeProcessor : YaeGeneralAttributeProcessor<DisplayAttribute>
{
    /// <inheritdoc/>
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, DisplayAttribute attribute)
    {
        // .NET 的 Exporter 不会自动处理 DisplayAttribute，所以我们需要自己添加。
        // 这给了我们更大的控制权。

        
        // 确保或覆盖 Name -> title 和 Description -> description
        string? name = attribute.GetName(); // 支持本地化资源
        if (!string.IsNullOrWhiteSpace(name))
        {
            schema["title"] = name;
        }

        string? description = attribute.GetDescription(); // 支持本地化资源
        if (!string.IsNullOrWhiteSpace(description))
        {
            schema["description"] = description;
        }

        string? prompt = attribute.GetPrompt(); // 支持本地化资源 (placeholder)
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            schema["ui:placeholder"] = prompt;
        }

        int? order = attribute.GetOrder();
        if (order.HasValue)
        {
            // 暂存排序值，供后续全局 ui:order 构建
            schema["x-temp-ui-order"] = order.Value;
        }
    }
}