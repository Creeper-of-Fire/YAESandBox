using System.ComponentModel.DataAnnotations;
using Namotion.Reflection;

namespace YAESandBox.Workflow.AIService.AiConfigSchema.SchemaProcessor;

// StringOptionsProcessor.cs
// For PropertyInfo
// For LINQ methods like Select
using NJsonSchema; // For JsonSchema, JsonObjectType
using NJsonSchema.Generation; // For ISchemaProcessor, SchemaProcessorContext
using YAESandBox.Workflow.AIService.AiConfigSchema; // For StringOptionsAttribute

/// <summary>
/// 处理带有 [Range] 的属性，
/// 为其生成枚举和/或指向自定义自动完成 Widget 的配置。
/// </summary>
public class RangeProcessor : ISchemaProcessor
{
    /// <summary>
    /// 处理给定的 Schema 上下文。
    /// </summary>
    /// <param name="context">Schema 生成的上下文。</param>
    public void Process(SchemaProcessorContext context)
    {
        var attribute = context.ContextualType.GetContextAttribute<RangeAttribute>(true);
        context.Schema.ExtensionData ??= new Dictionary<string, object?>();

        if (attribute == null) return;
        // context.Schema.ExtensionData["ui:widget"] = "SliderWidget";
        if (attribute is CustomRangeAttribute customRangeAttribute)
            context.Schema.MultipleOf = Convert.ToDecimal(customRangeAttribute.Step);
        else if (context.Schema.Type == JsonObjectType.Number)
        {
            decimal step = (Convert.ToDecimal(attribute.Maximum) - Convert.ToDecimal(attribute.Minimum)) / 100;
            context.Schema.MultipleOf = decimal.Min(step, 0.01m);
        }
    }
}