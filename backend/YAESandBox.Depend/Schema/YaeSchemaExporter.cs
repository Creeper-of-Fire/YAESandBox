using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Schema.SchemaProcessor; // 引用你所有的Attributes
using YAESandBox.Depend.Storage; // 引用你的JsonHelper

namespace YAESandBox.Depend.Schema;

/// <summary>
/// 使用 .NET 9 的 JsonSchemaExporter 生成与前端UI框架兼容的 JSON Schema。
/// 这是 NJsonSchema 方案的下一代实现，提供了对 Schema 生成过程更精细的控制。
/// </summary>
public static class YaeSchemaExporter
{
    private static void ConfigureDefaultOptions(YaeSchemaOptions options)
    {
        options.SchemaProcessors.AddRange([
            new DisplayAttributeProcessor(),
            new ClassLabelProcessor(),
            new CustomObjectWidgetRendererSchemaProcessor(),
            new HiddenInFormProcessor(),
            new DataTypeProcessor(),
            new RangeProcessor(),
            new StringOptionsProcessor(),
            new ComplexDefaultValueProcessor()
        ]);

        options.PostProcessSchema = BuildUiOrder;
    }

    public static string GenerateSchemaJson(Type type, Action<YaeSchemaOptions>? configureOptions = null)
    {
        var schema = GenerateSchema(type, configureOptions);
        // 使用自定义的JsonSerializerOptions来美化输出
        return schema.ToJsonString(YaeSandBoxJsonHelper.JsonSerializerOptions);
    }

    public static JsonNode GenerateSchema(Type type, Action<YaeSchemaOptions>? configureOptions = null)
    {
        // 1. 创建并配置 YaeSchemaOptions
        var options = new YaeSchemaOptions();
        ConfigureDefaultOptions(options); // 应用默认配置
        configureOptions?.Invoke(options); // 应用调用者提供的自定义配置

        var exporterOptions = new JsonSchemaExporterOptions
        {
            // 这是我们的魔法发生的地方
            TransformSchemaNode = (context, schema) => TransformNode(context, schema, options)
        };

        var schemaNode = YaeSandBoxJsonHelper.JsonSerializerOptions.GetJsonSchemaAsNode(type, exporterOptions);

        // 4. 执行后处理
        options.PostProcessSchema?.Invoke(schemaNode);

        return schemaNode;
    }

    // 稍后我们将在这里填充所有的逻辑
    private static JsonNode TransformNode(JsonSchemaExporterContext context, JsonNode schema, YaeSchemaOptions options)
    {
        // 如果 schema 是布尔值 (例如 `{"not": {}}` 会生成 `false`),
        // 我们需要将其转换为等价的 JsonObject 以便处理器可以操作。
        if (schema is not JsonObject schemaObj)
        {
            schemaObj = new JsonObject();
            if (schema.GetValue<bool>() == false)
            {
                schemaObj.Add("not", new JsonObject());
            }
            // 如果是 true, 它就是一个空对象 {}, 代表任何东西都匹配
        }

        // 依次调用所有处理器
        foreach (var processor in options.SchemaProcessors)
        {
            processor.Process(context, schemaObj);
        }

        return schemaObj;
    }

     /// <summary>
    /// 对根 Schema 进行后处理，根据属性上暂存的 "x-temp-ui-order" 元数据，
    /// 生成 "ui:order" 数组来控制前端表单的字段排序。
    /// </summary>
    /// <param name="rootSchemaNode">要处理的根 Schema 节点。</param>
    private static void BuildUiOrder(JsonNode rootSchemaNode)
    {
        // 确保我们正在处理的是一个对象类型的 Schema
        if (rootSchemaNode is not JsonObject rootSchema)
        {
            return;
        }

        // 尝试获取 "properties" 节点，如果不存在或不是一个对象，则无需排序
        if (!rootSchema.TryGetPropertyValue("properties", out var propertiesNode) || propertiesNode is not JsonObject properties)
        {
            return;
        }

        // 如果没有任何属性，也无需排序
        if (properties.Count == 0)
        {
            return;
        }

        int originalIndex = 0;
        var propertiesWithOrderInfo = properties
            .Select(propKvp => new
            {
                Name = propKvp.Key,
                Order = propKvp.Value is JsonObject propSchema &&
                        propSchema.TryGetPropertyValue("x-temp-ui-order", out var orderNode) &&
                        orderNode is JsonValue orderVal &&
                        orderVal.TryGetValue(out int orderInt)
                    ? orderInt
                    : int.MaxValue, // 没有显式 order 的属性排在最后
                OriginalIndex = originalIndex++, // 记录原始迭代顺序，用于稳定排序
                PropertySchema = propKvp.Value as JsonObject
            })
            .ToList();

        // 核心排序逻辑：首先按显式指定的 Order 升序，然后按原始顺序升序
        var orderedProperties = propertiesWithOrderInfo
            .OrderBy(p => p.Order)
            .ThenBy(p => p.OriginalIndex)
            .ToList();

        // 创建一个包含有序属性名的 JsonArray
        var uiOrderList = orderedProperties.Select(p => p.Name).ToJsonArray();
        
        // 将 "ui:order" 数组添加到根 Schema 中
        rootSchema["ui:order"] = uiOrderList;

        // 清理所有属性上临时的 "x-temp-ui-order" 元数据
        foreach (var prop in orderedProperties)
        {
            prop.PropertySchema?.Remove("x-temp-ui-order");
        }
    }
}