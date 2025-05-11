using System.Text.Json;
using System.Text.Json.Serialization;
using YAESandBox.Workflow.AIService.AiConfigSchema.SchemaProcessor;
using NJsonSchema;
using NJsonSchema.Generation;

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
        // settings.SchemaProcessors.Add(new CustomRangeProcessor());

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

        var orderedProperties = rootSchema.Properties
            .Select(prop => new
            {
                Name = prop.Key,
                Order = prop.Value.ExtensionData?.TryGetValue("x-temp-ui-order", out var orderVal) == true && orderVal is int orderInt
                    ? orderInt
                    : int.MaxValue,
                PropertySchema = prop.Value
            })
            .OrderBy(p => p.Order)
            .ThenBy(p => p.Name) // 确保相同 order 的属性有一个稳定排序
            .ToList();

        var uiOrderList = orderedProperties.Select(p => p.Name).ToList();

        // 如果所有属性都没有指定顺序，则不生成 ui:order (可选行为)
        // if (orderedProperties.All(p => p.Order == int.MaxValue) && orderedProperties.Count == rootSchema.Properties.Count)
        // {
        //     return;
        // }

        rootSchema.ExtensionData ??= new Dictionary<string, object?>();
        rootSchema.ExtensionData["ui:order"] = uiOrderList;

        // 清理临时的 x-temp-ui-order
        foreach (var prop in orderedProperties)
        {
            prop.PropertySchema.ExtensionData?.Remove("x-temp-ui-order");
        }
    }
}