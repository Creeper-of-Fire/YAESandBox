using System.Text.Json.Nodes;
using System.Text.Json.Schema;
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
            new DefaultValueProcessor(),
            new ComplexDefaultValueProcessor(),
            new InlineGroupProcessor(),
            new RequiredProcessor()
        ]);

        // 我们需要先执行扁平化，再构建UI顺序
        options.PostProcessSchema = rootSchema =>
        {
            BuildUiOrder(rootSchema);
            BuildRequiredArrays(rootSchema);
        };
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
            TransformSchemaNode = (context, schema) => TransformNode(context, schema, options)
        };

        var schemaNode = YaeSandBoxJsonHelper.JsonSerializerOptions.GetJsonSchemaAsNode(type, exporterOptions);

        // 4. 执行后处理
        options.PostProcessSchema?.Invoke(schemaNode);

        return schemaNode;
    }

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
    /// 递归地扫描整个 Schema 树，根据 "x-temp-is-required" 标记构建 "required" 数组。
    /// 这个方法首先找到所有对象类型的 Schema，然后为每一个独立构建它们的 required 列表。
    /// </summary>
    /// <param name="rootNode">要处理的根 Schema 节点。</param>
    internal static void BuildRequiredArrays(JsonNode rootNode)
    {
        // 1. 查找：收集 Schema 树中所有的对象节点
        var allObjectSchemas = new List<JsonObject>();
        FindAllObjectSchemas(rootNode, allObjectSchemas);

        // 2. 处理：为每个找到的对象节点构建其 "required" 数组
        foreach (var objectSchema in allObjectSchemas)
        {
            if (!objectSchema.TryGetPropertyValue("properties", out var propertiesNode) ||
                propertiesNode is not JsonObject properties)
            {
                continue;
            }

            // 收集当前对象的所有 required 属性名
            var requiredList = new List<string>();
            foreach (var property in properties)
            {
                if (property.Value is JsonObject propertySchema &&
                    propertySchema.Remove("x-temp-is-required", out var marker) &&
                    marker is JsonValue value && value.GetValue<bool>())
                {
                    requiredList.Add(property.Key);
                }
            }

            // 如果找到了，就创建 "required" 数组
            if (requiredList.Count > 0)
            {
                // 在这个阶段，不应该有已存在的 "required" 数组，
                // 因为这是第一个处理 "required" 的步骤。
                // 但为了健壮性，我们还是做一个简单的赋值。
                objectSchema["required"] = new JsonArray(requiredList.Select(r => JsonValue.Create(r)).ToArray<JsonNode?>());
            }
        }
    }

    /// <summary>
    /// 递归辅助函数，用于查找并收集一个 JsonNode 树中所有代表对象的 JsonObject。
    /// </summary>
    /// <param name="currentNode">当前正在访问的节点。</param>
    /// <param name="foundObjects">用于收集结果的列表。</param>
    private static void FindAllObjectSchemas(JsonNode? currentNode, List<JsonObject> foundObjects)
    {
        if (currentNode is not JsonObject currentObject)
        {
            return;
        }

        // 如果一个 JsonObject 包含 "properties" 键，我们就认为它是一个对象定义 Schema
        if (currentObject.ContainsKey("properties"))
        {
            foundObjects.Add(currentObject);
        }

        // 递归地探索所有子节点
        foreach (var property in currentObject)
        {
            if (property.Value is JsonObject childObject)
            {
                FindAllObjectSchemas(childObject, foundObjects);
            }
            else if (property.Value is JsonArray childArray)
            {
                foreach (var item in childArray)
                {
                    FindAllObjectSchemas(item, foundObjects);
                }
            }
        }
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
    internal static void BuildUiOrder(JsonNode rootSchemaNode)
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
        int depth = GetTypeInheritanceDepth(declaringType);
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