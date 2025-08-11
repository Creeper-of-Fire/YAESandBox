using System.Text.Json;
using System.Text.Json.Nodes;
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

internal class ComplexDefaultValueProcessor() : NormalAttributeProcessor<ComplexDefaultValueAttribute>((extensionData, attribute) =>
{
    object defaultValue = Activator.CreateInstance(attribute.ProviderType) ?? throw new InvalidOperationException();
    // 1. 将 C# 对象序列化为 JSON 字符串
    string jsonDefault = JsonSerializer.Serialize(defaultValue, YaeSandBoxJsonHelper.JsonSerializerOptions);
    // 将 JSON 字符串反序列化为一个普通的 Dictionary<string, object>
    // 这会创建一个“干净”的对象，不包含任何循环引用或父节点链接。
    var plainObjectDefault = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonDefault, YaeSandBoxJsonHelper.JsonSerializerOptions);

    extensionData["default"] = plainObjectDefault;
});