using System.Text.Json;
using System.Text.Json.Serialization;
using YAESandBox.Workflow.Config;

namespace YAESandBox.Workflow.Utility;

/// <summary>
/// 自定义的 JsonConverter 用于 <see cref="AbstractModuleConfig"/> 接口的多态反序列化。
/// </summary>
internal class ModuleConfigConverter : JsonConverter<AbstractModuleConfig>
{
    // 使用 nameof 获取规范的属性名称，以增强代码健壮性。
    // 这是我们期望在 JSON 中查找的属性名，比较时会忽略大小写。
    private const string ExpectedModuleTypePropertyName = nameof(AbstractModuleConfig.ModuleType);

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        // 此转换器仅用于 AbstractModuleConfig 接口。
        return typeToConvert == typeof(AbstractModuleConfig);
    }

    /// <inheritdoc />
    public override AbstractModuleConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            // 预期的 JSON 对象起始符号 '{' 未找到。
            throw new JsonException($"反序列化 AbstractModuleConfig 失败：预期的 JSON 对象起始符号 '{{' 未找到，当前 Token 类型为 {reader.TokenType}。");
        }

        // 为了预读 ModuleType 属性，我们先将 JSON 对象解析为 JsonElement。
        // 这样不会影响原始 reader 的状态，使得后续可以正确反序列化整个对象到具体类型。
        using var jsonDocument = JsonDocument.ParseValue(ref reader);
        var jsonObject = jsonDocument.RootElement;

        string? moduleTypeNameFromInput = null;
        bool moduleTypePropertyFound = false;
        string actualPropertyNameFound = string.Empty; // 用于错误消息

        // 遍历 JSON 对象的属性，不区分大小写地查找 ModuleType 属性。
        foreach (var property in jsonObject.EnumerateObject())
        {
            // JsonProperty.NameEquals 方法支持不区分大小写的比较。
            if (string.Equals(property.Name, ExpectedModuleTypePropertyName, StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    moduleTypeNameFromInput = property.Value.GetString();
                }
                else
                {
                    // ModuleType 属性的值不是字符串类型。
                    throw new JsonException($"反序列化 AbstractModuleConfig 失败：属性 '{property.Name}' 的值必须是字符串，但实际类型为 {property.Value.ValueKind}。");
                }

                moduleTypePropertyFound = true;
                actualPropertyNameFound = property.Name; // 记录实际找到的属性名
                break; // 找到后即可退出循环
            }
        }

        if (!moduleTypePropertyFound)
        {
            // 未能在JSON对象中找到期望的 ModuleType 属性。
            throw new JsonException($"反序列化 AbstractModuleConfig 失败：无法在JSON对象中找到属性 '{ExpectedModuleTypePropertyName}' (忽略大小写)，该属性用于确定具体的模块配置类型。");
        }

        if (string.IsNullOrWhiteSpace(moduleTypeNameFromInput))
        {
            // ModuleType 属性的值为空或空白。
            throw new JsonException($"反序列化 AbstractModuleConfig 失败：属性 '{actualPropertyNameFound}' 的值不能为空或仅包含空白字符。");
        }

        // 使用辅助类查找对应的具体类型。
        // ModuleConfigTypeResolver 内部也应该使用不区分大小写的比较。
        var concreteType = ModuleConfigTypeResolver.FindModuleConfigType(moduleTypeNameFromInput);

        if (concreteType == null)
        {
            // 未找到与 ModuleType 值对应的 .NET 实现类型。
            throw new JsonException(
                $"反序列化 AbstractModuleConfig 失败：未找到与模块类型 '{moduleTypeNameFromInput}' (来自属性 '{actualPropertyNameFound}') 对应的 .NET 实现类型。请确保存在一个名为 '{moduleTypeNameFromInput}' (忽略大小写) 且实现了 AbstractModuleConfig 接口的公共非抽象类。");
        }

        // 使用 JsonElement 的 Deserialize 方法将 JSON 对象反序列化为找到的具体类型。
        // 这会利用 JsonSerializer 的所有功能，包括处理其他属性和嵌套对象。
        var moduleConfig = jsonObject.Deserialize(concreteType, options) as AbstractModuleConfig;

        if (moduleConfig == null && jsonObject.EnumerateObject().Any()) // 如果反序列化结果为 null 但 JSON 对象非空，可能类型不匹配或构造函数问题
        {
            // 这种情况理论上不应发生，因为 concreteType 是从 ModuleConfigTypeResolver 获取的，且 Deserialize 应该能处理。
            // 但作为健壮性检查，如果发生，则指示更深层次的问题。
            throw new JsonException($"反序列化 AbstractModuleConfig 失败：成功将 JSON 元素反序列化为类型 '{concreteType.FullName}' 后得到 null。请检查该类型的构造函数和属性设置。");
        }

        return moduleConfig;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AbstractModuleConfig value, JsonSerializerOptions options)
    {
        // 对于序列化，System.Text.Json 在传递实际类型（value.GetType()）时能正确处理。
        // 它会序列化具体类型的所有属性，包括 ModuleType 属性。
        // ModuleType 属性的名称将根据其在具体类中的定义（通常是 "ModuleType"，因为 nameof(AbstractModuleConfig.ModuleType) 是 "ModuleType"）
        // 以及 JsonSerializerOptions 中的命名策略（如 PropertyNamingPolicy）来确定。
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}