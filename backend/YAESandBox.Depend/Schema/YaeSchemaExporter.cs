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
            new InheritanceOrderProcessor(), 
            new DisplayAttributeProcessor(),
            new ClassLabelProcessor(),
            new CustomObjectWidgetRendererSchemaProcessor(),
            new HiddenInFormProcessor(),
            new DataTypeProcessor(),
            new RangeProcessor(),
            new StringOptionsProcessor(),
            new ComplexDefaultValueProcessor(),
            new InlineGroupProcessor(),
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
    /// 对根 Schema 进行后处理，根据属性上暂存的元数据，
    /// 生成 "ui:order" 数组来控制前端表单的字段排序。
    /// 排序规则:
    /// 1. 按显式指定的 Order (来自 DisplayAttribute 等) 升序。
    /// 2. 按继承深度升序（父类属性在前）。
    /// 3. 按原始声明顺序升序（作为稳定排序的保障）。
    /// </summary>
    /// <param name="rootSchemaNode">要处理的根 Schema 节点。</param>
    private static void BuildUiOrder(JsonNode rootSchemaNode)
    {
        if (rootSchemaNode is not JsonObject rootSchema ||
            !rootSchema.TryGetPropertyValue("properties", out var propertiesNode) ||
            propertiesNode is not JsonObject properties ||
            properties.Count == 0)
        {
            return;
        }

        int originalIndex = 0;
        var propertiesWithOrderInfo = properties
            .Select(propKvp =>
            {
                var propSchema = propKvp.Value as JsonObject;
                
                // 提取显式 Order
                int order = int.MaxValue;
                if (propSchema != null &&
                    propSchema.TryGetPropertyValue("x-temp-ui-order", out var orderNode) &&
                    orderNode is JsonValue orderVal &&
                    orderVal.TryGetValue(out int orderInt))
                {
                    order = orderInt;
                }

                // 提取继承深度
                int depth = 0; // 默认深度为0
                if (propSchema != null &&
                    propSchema.TryGetPropertyValue("x-temp-inheritance-depth", out var depthNode) &&
                    depthNode is JsonValue depthVal &&
                    depthVal.TryGetValue(out int depthInt))
                {
                    depth = depthInt;
                }

                return new
                {
                    Name = propKvp.Key,
                    Order = order,
                    InheritanceDepth = depth,
                    OriginalIndex = originalIndex++,
                    PropertySchema = propSchema
                };
            })
            .ToList();

        // 核心排序逻辑：显式Order -> 继承深度(父类在前) -> 原始顺序
        var orderedProperties = propertiesWithOrderInfo
            .OrderBy(p => p.Order)
            .ThenBy(p => p.InheritanceDepth) // 深度越小，越是基类，排在越前面
            .ThenBy(p => p.OriginalIndex)
            .ToList();

        // 创建一个包含有序属性名的 JsonArray
        var uiOrderList = orderedProperties.Select(p => p.Name).ToJsonArray();

        // 将 "ui:order" 数组添加到根 Schema 中
        rootSchema["ui:order"] = uiOrderList;

        // 清理所有属性上临时的元数据
        foreach (var prop in orderedProperties)
        {
            prop.PropertySchema?.Remove("x-temp-ui-order");
            prop.PropertySchema?.Remove("x-temp-inheritance-depth");
        }
    }
}

/// <summary>
/// 一个 Schema 处理器，用于计算属性的继承深度并将其作为临时元数据添加到 Schema 中。
/// 这个深度信息稍后会被 BuildUiOrder 方法用来确保基类属性排在派生类属性之前。
/// </summary>
internal class InheritanceOrderProcessor : IYaeSchemaProcessor
{
    public void Process(JsonSchemaExporterContext context, JsonObject schema)
    {
        // 此处理器仅对属性有效
        if (context.PropertyInfo is null)
        {
            return;
        }

        // 获取属性的声明类型
        var declaringType = context.PropertyInfo.DeclaringType;

        // 计算并存储继承深度
        var depth = GetTypeInheritanceDepth(declaringType);
        schema["x-temp-inheritance-depth"] = depth;
    }

    /// <summary>
    /// 计算一个类型在继承链中的深度。`object` 的深度为 0。
    /// </summary>
    private static int GetTypeInheritanceDepth(Type type)
    {
        int depth = 0;
        var current = type;
        while (current.BaseType != null)
        {
            depth++;
            current = current.BaseType;
        }
        return depth;
    }
}