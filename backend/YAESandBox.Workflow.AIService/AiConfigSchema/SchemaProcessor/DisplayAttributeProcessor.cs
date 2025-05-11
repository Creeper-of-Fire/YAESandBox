using System.ComponentModel.DataAnnotations;
using Namotion.Reflection;
using NJsonSchema.Generation;

namespace YAESandBox.Workflow.AIService.AiConfigSchema.SchemaProcessor;

/// <summary>
/// 处理 [DisplayAttribute] 为 Schema 添加 title, description, placeholder, 和排序信息。
/// </summary>
public class DisplayAttributeProcessor : ISchemaProcessor
{
    public void Process(SchemaProcessorContext context)
    {
        if (context.ContextualType.GetContextAttribute<DisplayAttribute>(true) is not { } displayAttribute)
            return;

        // NJsonSchema 通常会处理 Name -> title 和 Description -> description
        // 但我们可以确保或覆盖（如果需要）
        var name = displayAttribute.GetName(); // 支持本地化资源
        if (!string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(context.Schema.Title))
        {
            context.Schema.Title = name;
        }

        var description = displayAttribute.GetDescription(); // 支持本地化资源
        if (!string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(context.Schema.Description))
        {
            context.Schema.Description = description;
        }

        var prompt = displayAttribute.GetPrompt(); // 支持本地化资源 (placeholder)
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            context.Schema.ExtensionData ??= new Dictionary<string, object?>();
            context.Schema.ExtensionData["ui:placeholder"] = prompt;
        }

        var order = displayAttribute.GetOrder();
        if (order.HasValue)
        {
            context.Schema.ExtensionData ??= new Dictionary<string, object>();
            // 暂存排序值，供后续全局 ui:order 构建
            context.Schema.ExtensionData["x-temp-ui-order"] = order.Value;
        }
    }
}