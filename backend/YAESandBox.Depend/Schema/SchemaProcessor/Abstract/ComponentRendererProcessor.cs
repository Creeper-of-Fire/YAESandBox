using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace YAESandBox.Depend.Schema.SchemaProcessor.Abstract;

/// <summary>
/// 一个通用的、可重用的处理器，用于处理那些可以同时应用于类型和属性、
/// 并根据应用位置需要不同 Schema 键的 Attribute。
/// </summary>
/// <param name="typeKey">当 Attribute 应用于类型时，要写入 Schema 的键。</param>
/// <param name="propertyKey">当 Attribute 应用于属性时，要写入 Schema 的键。</param>
/// <param name="valueSelector">一个委托，用于从 Attribute 实例中提取要写入的值。</param>
/// <typeparam name="TAttribute">要处理的 Attribute 类型。</typeparam>
public class ComponentRendererProcessor<TAttribute>(string typeKey, string propertyKey, Func<TAttribute, string> valueSelector)
    : IYaeSchemaProcessor where TAttribute : Attribute
{
    private readonly string TypeKey = typeKey;
    private readonly string PropertyKey = propertyKey;
    private readonly Func<TAttribute, string> ValueSelector = valueSelector;

    /// <inheritdoc />
    public void Process(JsonSchemaExporterContext context, JsonObject schema)
    {
        // 核心逻辑：属性上的特性优先级高于类型上的特性。

        // 1. 检查属性级别
        var attribute = context.PropertyInfo?.AttributeProvider.GetCustomAttribute<TAttribute>();
        if (attribute is not null)
        {
            schema[this.PropertyKey] = this.ValueSelector(attribute);
            // 找到并处理后，直接返回，不再处理类型级别的特性
            return;
        }

        // 2. 如果属性上没有，再检查类型级别
        // 这会处理纯类型上下文，以及属性的类型上带有特性的情况
        var typeAttribute = CustomAttributeExtensions.GetCustomAttribute<TAttribute>(context.TypeInfo.Type);
        if (typeAttribute is not null)
        {
            schema[this.TypeKey] = this.ValueSelector(typeAttribute);
        }
    }
}