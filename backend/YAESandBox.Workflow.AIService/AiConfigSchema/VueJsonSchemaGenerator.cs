using System.Text.Json;
using System.Text.Json.Serialization;
using NJsonSchema;
using NJsonSchema.Generation;
using YAESandBox.Workflow.AIService.AiConfigSchema.SchemaProcessor;

namespace YAESandBox.Workflow.AIService.AiConfigSchema;

/// <summary>
/// 生成与 vue-json-schema-form 兼容的 JSON Schema。
/// </summary>
public static class VueFormSchemaGenerator
{
    public static string GenerateSchemaJson(Type type, Action<SystemTextJsonSchemaGeneratorSettings>? configureSettings = null)
    {
        var schema = GenerateSchema(type, configureSettings);
        return schema.ToJson();
    }

    public static JsonSchema GenerateSchema(Type type, Action<SystemTextJsonSchemaGeneratorSettings>? configureSettings = null)
    {
        // 1. 创建 System.Text.Json 的 JsonSerializerOptions，这将驱动 NJsonSchema 的行为
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }, // C# enum 转字符串
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // 忽略null值
            ReferenceHandler = ReferenceHandler.Preserve
            // WriteIndented = true, // 这个主要用于最终序列化，对生成过程影响不大
        };

        // 2. 实例化 SystemTextJsonSchemaGeneratorSettings
        // 它会使用传入的 serializerOptions 来获取反射服务
        var settings = new SystemTextJsonSchemaGeneratorSettings()
        {
            SerializerOptions = serializerOptions,
            // SchemaType = SchemaType.JsonSchema, // 默认就是 JsonSchema
            DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.Null, // 默认可空，除非有[Required]或NRT非空
            GenerateAbstractProperties = true, // 用于 JsonPolymorphic (在 System.Text.Json 中由 [JsonDerivedType] 处理)
            FlattenInheritanceHierarchy = true,
            // GenerateExamples = true, // 如果需要示例
            // TypeNameGenerator = new DefaultTypeNameGenerator(), // 可以自定义类型名生成
            // SchemaNameGenerator = new DefaultSchemaNameGenerator(), // 可以自定义 Schema $id 生成
            // ExcludedTypeNames = [],
            // UseXmlDocumentation = true, // 如果需要从 XML 文档注释生成 description
        };


        // 添加自定义 Schema Processors
        settings.SchemaProcessors.Add(new DisplayAttributeProcessor());
        settings.SchemaProcessors.Add(new StringOptionsProcessor());
        settings.SchemaProcessors.Add(new RangeProcessor());

        // 允许外部进一步配置
        configureSettings?.Invoke(settings);

        // 3. 创建 JsonSchemaGenerator，现在可以传入具体的 settings 对象
        var generator = new JsonSchemaGenerator(settings);
        var rootSchema = generator.Generate(type);

        // 4. 后处理：构建顶层的 ui:order
        BuildUiOrder(rootSchema);

        return rootSchema;
    }

    private static void BuildUiOrder(JsonSchema rootSchema)
    {
        if (!rootSchema.Properties.Any())
        {
            return;
        }

        int originalIndex = 0; // 用于记录原始迭代顺序
        var propertiesWithOrderInfo = rootSchema.Properties
            .Select(propKvp => new
            {
                Name = propKvp.Key,
                Order = propKvp.Value.ExtensionData?.TryGetValue("x-temp-ui-order", out object? orderVal) == true &&
                        orderVal is int orderInt
                    ? orderInt
                    : int.MaxValue, // 没有显式 order 的排在后面
                OriginalIndex = originalIndex++, // 记录原始迭代顺序
                PropertySchema = propKvp.Value
            })
            .ToList(); // 物化列表，确保 OriginalIndex 被正确赋值

        var orderedProperties = propertiesWithOrderInfo
            .OrderBy(p => p.Order) // 主要排序：按显式 Order
            .ThenBy(p => p.OriginalIndex) // 次要排序：按原始迭代顺序
            .ToList();

        var uiOrderList = orderedProperties.Select(p => p.Name).ToList();

        // 可选：如果所有属性都没有有效的显式 Order (即 Order 都是 int.MaxValue)
        // 并且列表不为空，那么 uiOrderList 已经是按 OriginalIndex 排序的了。
        // 这种情况下，是否添加 ui:order 取决于是否总是希望显式指定顺序。
        // 如果希望只有在至少有一个显式 Order 时才添加 ui:order，可以取消下面的注释。
        // if (orderedProperties.All(p => p.Order == int.MaxValue) && orderedProperties.Any())
        // {
        //     // 如果不希望在这种情况下添加 ui:order，可以在这里返回或不设置 ExtensionData["ui:order"]
        //     // 但通常，即使没有显式 order，提供一个稳定的顺序（基于迭代序）也是有益的。
        // }

        if (uiOrderList.Any()) // 仅当有属性时才添加 ui:order
        {
            rootSchema.ExtensionData ??= new Dictionary<string, object?>();
            rootSchema.ExtensionData["ui:order"] = uiOrderList;
        }

        // 清理临时的 x-temp-ui-order
        foreach (var prop in orderedProperties)
        {
            prop.PropertySchema.ExtensionData?.Remove("x-temp-ui-order");
        }
    }
}