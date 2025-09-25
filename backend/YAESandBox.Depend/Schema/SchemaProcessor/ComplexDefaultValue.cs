using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using YAESandBox.Depend.Schema.SchemaProcessor.Abstract;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Depend.Schema.SchemaProcessor;

/// <summary>
/// 指示一个属性的复杂默认值应由此特性的提供者类型来生成。
/// 用于在生成 Schema 时注入一个 'default' 对象。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ComplexDefaultValueAttribute(Type providerType) : Attribute
{
    /// <summary>
    /// 类型
    /// </summary>
    public Type ProviderType { get; } = providerType;
}

internal class ComplexDefaultValueProcessor : YaePropertyAttributeProcessor<ComplexDefaultValueAttribute>
{
    /// <inheritdoc />
    protected override void ProcessAttribute(JsonSchemaExporterContext context, JsonObject schema, ComplexDefaultValueAttribute attribute)
    {
        object defaultValue = Activator.CreateInstance(attribute.ProviderType) ?? throw new InvalidOperationException();
        
        var defaultNode = JsonSerializer.SerializeToNode(defaultValue, YaeSandBoxJsonHelper.JsonSerializerOptions);

        if (defaultNode is not null) 
            schema["default"] = defaultNode;
    }
}