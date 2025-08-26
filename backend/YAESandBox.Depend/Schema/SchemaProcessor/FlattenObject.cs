using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 定义属性名称冲突时的解决策略。
/// </summary>
public enum FlattenConflictResolution
{
    /// <summary>
    /// 如果发生属性名称冲突，则抛出 InvalidOperationException。这是最安全、最明确的默认行为。
    /// </summary>
    ThrowOnError,

    /// <summary>
    /// 如果发生冲突，保留父对象中已存在的属性，忽略子对象中的同名属性。
    /// </summary>
    PreferParent,

    /// <summary>
    /// 如果发生冲突，使用子对象中的属性覆盖父对象中的同名属性。
    /// </summary>
    PreferChild
}

/// <summary>
/// 指示 Schema 导出器应将此属性对象的属性“扁平化”到其父级 Schema 中，
/// 而不是创建一个嵌套的对象。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class FlattenAttribute(FlattenConflictResolution conflictResolution = FlattenConflictResolution.ThrowOnError) : Attribute
{
    /// <summary>
    /// 获取当扁平化过程中发生属性名称冲突时的解决策略。
    /// </summary>
    public FlattenConflictResolution ConflictResolution { get; } = conflictResolution;
}

/// <summary>
/// 在 Schema 生成期间，为标记了 [Flatten] 特性的属性添加一个临时标记 "x-temp-flatten"。
/// 真正的扁平化逻辑在一个后处理步骤中执行。
/// </summary>
internal class FlattenMarkerProcessor : YaePropertyAttributeProcessor<FlattenAttribute>
{
    public const string FlattenMarker = "x-temp-flatten";

    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, FlattenAttribute attribute)
    {
        // 验证被标记的属性是否是一个对象类型，如果不是，扁平化没有意义。
        if (!IsObjectSchema(schema))
        {
            // 或者记录一个警告
            // Console.WriteLine($"[WARNING] [Flatten] attribute on property '{context.PropertyInfo?.Name}' of type '{context.PropertyInfo?.PropertyType.Name}' which is not an object. The attribute will be ignored.");
            return;
        }

        // 将冲突解决策略作为标记的值存储起来，以便后处理器使用。
        schema[FlattenMarker] = attribute.ConflictResolution.ToString();
    }
    
    /// <summary>
    /// 检查一个 Schema JsonObject 是否代表一个 "object" 类型。
    /// 这能正确处理 "type": "object" 和 "type": ["object", "null"] 的情况。
    /// </summary>
    /// <param name="schema">要检查的 Schema 对象。</param>
    /// <returns>如果类型是或包含 "object"，则为 true。</returns>
    private static bool IsObjectSchema(JsonObject schema)
    {
        if (!schema.TryGetPropertyValue("type", out var typeNode) || typeNode is null)
        {
            // 如果没有 'type' 关键字，但有 'properties'，通常也认为它是一个对象。
            // 这是一种更宽松的检查，可以增加健壮性。
            return schema.ContainsKey("properties");
        }

        // 情况1： "type": "object"
        if (typeNode is JsonValue typeValue)
        {
            return typeValue.TryGetValue<string>(out var typeString) && typeString == "object";
        }

        // 情况2： "type": ["object", "null"]
        if (typeNode is JsonArray typeArray)
        {
            // 检查数组中是否包含值为 "object" 的 JsonValue
            return typeArray.Any(node =>
                node is JsonValue value &&
                value.TryGetValue<string>(out var str) &&
                str == "object");
        }

        return false;
    }
}