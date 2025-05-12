using System.ComponentModel;
using Namotion.Reflection;

namespace YAESandBox.Workflow.AIService.AiConfigSchema.SchemaProcessor;

// StringOptionsProcessor.cs
// For PropertyInfo
// For LINQ methods like Select
using NJsonSchema; // For JsonSchema, JsonObjectType
using NJsonSchema.Generation; // For ISchemaProcessor, SchemaProcessorContext
using YAESandBox.Workflow.AIService.AiConfigSchema; // For StringOptionsAttribute

/// <summary>
/// 处理带有 [StringOptionsAttribute] 的属性，
/// 为其生成枚举和/或指向自定义自动完成 Widget 的配置。
/// </summary>
public class StringOptionsProcessor : ISchemaProcessor
{
    /// <summary>
    /// 处理给定的 Schema 上下文。
    /// </summary>
    /// <param name="context">Schema 生成的上下文。</param>
    public void Process(SchemaProcessorContext context)
    {
        // 获取属性上的 StringOptionsAttribute
        // context.ContextualAttributes 包含与当前类型/成员关联的所有特性
        var optionsAttribute = context.ContextualType.GetContextAttribute<StringOptionsAttribute>(true);

        if (optionsAttribute == null ||
            context.Schema.Type.HasFlag(JsonObjectType.Object) ||
            context.Schema.Type.HasFlag(JsonObjectType.Array))
        {
            return;
        }

        // 确认属性是字符串类型，如果不是，则此 Processor 不适用或发出警告
        // NJsonSchema 通常会根据 C# 属性类型正确设置 context.Schema.Type
        // 这里我们假设它已经是 string 或将要是 string
        if (!context.Schema.Type.HasFlag(JsonObjectType.String) && !context.Schema.Type.HasFlag(JsonObjectType.Null)) // 允许可空字符串
        {
            // 可以选择记录一个警告，或者如果类型绝对应该是 string，则强制设置
            // Console.WriteLine($"警告: StringOptionsAttribute 应用于非字符串类型属性 '{context.PropertyInfo?.Name}'。Schema 类型: {context.Schema.Type}");
            // 为了确保，我们强制它为 string，因为 StringOptionsAttribute 暗示了这一点。
            context.Schema.Type = JsonObjectType.String;
            if (context.ContextualType.IsNullableType) // 如果原始C#类型是可空的 string?
            {
                context.Schema.Type |= JsonObjectType.Null;
            }
        }

        context.Schema.ExtensionData ??= new Dictionary<string, object?>();

        // 1. 处理静态选项 (作为 Schema 的 enum/enumNames)
        // 这些选项将由自定义 Widget 读取作为初始/静态建议
        if (optionsAttribute.Options.Length > 0)
        {
            context.Schema.Enumeration.Clear(); // 清除可能由 NJsonSchema 默认生成的（例如，如果属性是C# enum）
            foreach (string val in optionsAttribute.Options.Select(o => o.Value))
                context.Schema.Enumeration.Add(val);

            // **** 关键修正：确保生成 "enumNames" ****
            context.Schema.EnumerationNames.Clear(); // 清理 NJsonSchema 可能已填充的 EnumerationNames

            // 直接将标签列表添加到 ExtensionData["enumNames"]，这是最可靠的方式
            // 以确保最终 JSON 中是 "enumNames" 而不是 "x-enumNames" 或其他。
            context.Schema.ExtensionData["enumNames"] = optionsAttribute.Options.Select(o => o.Label).ToList();

            var defaultAttribute = context.ContextualType.GetContextAttribute<DefaultValueAttribute>(true);
            if (defaultAttribute == null)
                context.Schema.ExtensionData["default"] = optionsAttribute.Options.First().Value;
        }

        // // 2. 指定使用自定义的自动完成 Widget
        // if (attribute.IsEditableSelectOptions)
        //     context.Schema.ExtensionData["ui:widget"] = "MyCustomStringAutoComplete"; // 前端自定义组件的名称
        // else
        //     context.Schema.ExtensionData["ui:widget"] = "RadioWidget";

        var uiOptions = SchemaProcessorHelper.GetOrCreateUiOptions(context.Schema);

        if (optionsAttribute.IsEditableSelectOptions)
            uiOptions["isEditableSelectOptions"] = true;

        // 3. 如果有 OptionsProviderEndpoint，则将其传递给 Widget
        if (!string.IsNullOrWhiteSpace(optionsAttribute.OptionsProviderEndpoint))
            uiOptions["remoteSearchEndpoint"] = optionsAttribute.OptionsProviderEndpoint;

        // 备注: IsEditableSelectOptions 属性目前没有直接用于改变 Schema 生成逻辑，
        // 因为我们选择的 MyCustomStringAutoComplete Widget 本身就是可编辑的。
        // 如果需要，可以将 attribute.IsEditableSelectOptions 的值也通过 uiOptions 传递给 Widget。
        // var uiOpt = SchemaProcessorHelper.GetOrCreateUiOptions(context.Schema);
        // uiOpt["isEditableFlagForWidget"] = attribute.IsEditableSelectOptions;
    }
}