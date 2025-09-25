using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace YAESandBox.Depend.Schema.SchemaProcessor.Abstract;

/// <summary>
/// 一个抽象基类，用于简化处理【仅应用于属性】的 Attribute。
/// </summary>
/// <typeparam name="T">要查找和处理的 Attribute 类型，该 Attribute 应标记在属性上。</typeparam>
public abstract class YaePropertyAttributeProcessor<T> : IYaeSchemaProcessor where T : Attribute
{
    /// <inheritdoc />
    public void Process(JsonSchemaExporterContext context, JsonObject schema)
    {
        // 如果当前上下文不是属性，则直接跳过。
        var attribute = context.PropertyInfo?.AttributeProvider?.GetCustomAttribute<T>(inherit: true);

        if (attribute != null)
        {
            this.ProcessAttribute(context, schema, attribute);
        }
    }

    /// <summary>
    /// 用于处理属性的 Attribute。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="schema"></param>
    /// <param name="attribute"></param>
    protected abstract void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, T attribute);
}

/// <summary>
/// 一个抽象基类，用于简化处理【仅应用于类型（类、接口、记录）】的 Attribute。
/// </summary>
/// <typeparam name="T">要查找和处理的 Attribute 类型，该 Attribute 应标记在类型上。</typeparam>
public abstract class YaeTypeAttributeProcessor<T> : IYaeSchemaProcessor where T : Attribute
{
    /// <inheritdoc />
    public void Process(JsonSchemaExporterContext context, JsonObject schema)
    {
        // 如果当前上下文是属性，则直接跳过。
        // 我们只关心根类型或嵌套类型的定义。
        if (context.PropertyInfo is not null)
            return;

        var attribute = context.TypeInfo.Type.GetCustomAttribute<T>(inherit: true);

        if (attribute is not null)
        {
            this.ProcessAttribute(context, schema, attribute);
        }
    }

    /// <summary>
    /// 用于处理类型的 Attribute。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="schema"></param>
    /// <param name="attribute"></param>
    protected abstract void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, T attribute);
}

/// <summary>
/// 对于类型/接口等和属性都同等进行处理的 Attribute
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class YaeGeneralAttributeProcessor<T> : IYaeSchemaProcessor where T : Attribute
{
    /// <inheritdoc />
    public void Process(JsonSchemaExporterContext context, JsonObject schema)
    {
        var attributeProvider = context.PropertyInfo?.AttributeProvider ?? context.TypeInfo.Type;

        // 正确使用 GetCustomAttributes，并用 OfType 和 FirstOrDefault 来筛选
        var attribute = attributeProvider.GetCustomAttribute<T>(inherit: true);

        if (attribute is not null)
        {
            this.ProcessAttribute(context, schema, attribute);
        }
    }

    /// <summary>
    /// 处理属性或类型上的 Attribute
    /// </summary>
    /// <param name="context"></param>
    /// <param name="schema"></param>
    /// <param name="attribute"></param>
    protected abstract void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, T attribute);
}