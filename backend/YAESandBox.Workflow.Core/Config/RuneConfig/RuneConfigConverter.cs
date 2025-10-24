using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Workflow.Core.Config.RuneConfig;

/// <summary>
/// 自定义的 JsonConverter 用于 <see cref="AbstractRuneConfig"/> 接口的多态反序列化。
/// </summary>
internal class RuneConfigConverter : JsonConverter<AbstractRuneConfig>
{
    // 使用 nameof 获取规范的属性名称，以增强代码健壮性。
    // 这是我们期望在 JSON 中查找的属性名，比较时会忽略大小写。
    private const string ExpectedRuneTypePropertyName = nameof(AbstractRuneConfig.RuneType);

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        // 此转换器仅用于 AbstractRuneConfig 接口。
        return typeToConvert == typeof(AbstractRuneConfig);
    }

    /// <inheritdoc />
    public override AbstractRuneConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            // 预期的 JSON 对象起始符号 '{' 未找到。
            throw new JsonException($"反序列化 AbstractRuneConfig 失败：预期的 JSON 对象起始符号 '[[' 未找到，当前 Token 类型为 {reader.TokenType}。");
        }

        // 为了预读 RuneType 属性，我们先将 JSON 对象解析为 JsonElement。
        // 这样不会影响原始 reader 的状态，使得后续可以正确反序列化整个对象到具体类型。
        using var jsonDocument = JsonDocument.ParseValue(ref reader);
        var jsonObject = jsonDocument.RootElement;


        try
        {
            return TryCreateRuneConfig(jsonObject, options);
        }
        catch (JsonException jsonEx)
        {
            // 捕获所有内部的JsonException，例如类型不匹配
            string typeName = jsonObject.TryGetProperty(ExpectedRuneTypePropertyName, out var prop) &&
                              prop.ValueKind == JsonValueKind.String
                ? prop.GetString() ?? "未知"
                : "未知";
            return CreateFallback(jsonObject, $"JSON反序列化到目标类型时出错。{jsonEx.ToFormattedString()}", typeName);
        }
        catch (Exception ex)
        {
            // 捕获其他意外异常
            string typeName =
                jsonObject.TryGetProperty(ExpectedRuneTypePropertyName, out var prop) && prop.ValueKind == JsonValueKind.String
                    ? prop.GetString() ?? "未知"
                    : "未知";
            return CreateFallback(jsonObject, $"发生意外错误。{ex.ToFormattedString()}", typeName);
        }
    }

    private static AbstractRuneConfig? TryCreateRuneConfig(JsonElement jsonObject, JsonSerializerOptions options)
    {
        // 不区分大小写地查找 RuneType 属性
        var runeTypeProperty = jsonObject.EnumerateObject()
            .FirstOrDefault(p => string.Equals(p.Name, ExpectedRuneTypePropertyName, StringComparison.OrdinalIgnoreCase));

        if (runeTypeProperty.Name == null) // 即 FirstOrDefault 返回了默认值
        {
            return CreateFallback(jsonObject,
                $"反序列化失败：JSON对象中缺少必需的 '{ExpectedRuneTypePropertyName}' 属性。");
        }

        string actualPropertyNameFound = runeTypeProperty.Name; // 用于错误消息
        if (runeTypeProperty.Value.ValueKind != JsonValueKind.String)
        {
            return CreateFallback(jsonObject,
                $"反序列化失败：属性 '{actualPropertyNameFound}' 的值必须是字符串，但实际类型为 {runeTypeProperty.Value.ValueKind}。", "类型错误");
        }

        string? runeTypeNameFromInput = runeTypeProperty.Value.GetString();
        if (string.IsNullOrWhiteSpace(runeTypeNameFromInput))
        {
            return CreateFallback(jsonObject,
                $"反序列化失败：属性 '{actualPropertyNameFound}' 的值不能为空。", "空类型名");
        }


        // 2. 查找对应的 .NET 类型
        var concreteType = RuneConfigTypeResolver.FindRuneConfigType(runeTypeNameFromInput);
        if (concreteType == null)
        {
            return CreateFallback(jsonObject,
                $"反序列化失败：未找到与符文类型 '{runeTypeNameFromInput}' 对应的.NET实现。", runeTypeNameFromInput);
        }

        // 3. 尝试反序列化为具体类型
        // 这里是可能抛出异常的地方，例如属性类型不匹配
        if (jsonObject.Deserialize(concreteType, options) is not AbstractRuneConfig runeConfig)
        {
            // 这种情况很少见，但为了健壮性也处理一下
            return CreateFallback(jsonObject,
                $"反序列化失败：成功将JSON元素反序列化为类型 '{concreteType.FullName}' 后得到null。", runeTypeNameFromInput);
        }

        return runeConfig;
    }

    /// <summary>
    /// 当尝试将 JSON 反序列化为 AbstractRuneConfig 时发生错误，于是创建一个UnknownRuneConfig
    /// </summary>
    private static UnknownRuneConfig CreateFallback(JsonElement jsonObject, string errorMessage, string? originalRuneType = null)
    {
        // 尝试从原始JSON中读取通用属性，以便在UI中更好地识别
        string? name = jsonObject.TryGetProperty(nameof(AbstractRuneConfig.Name), out var nameProp) ? nameProp.GetString() : "未知名称";
        string? configId = jsonObject.TryGetProperty(nameof(AbstractRuneConfig.ConfigId), out var idProp)
            ? idProp.GetString()
            : Guid.NewGuid().ToString();
        
        // 1. 先用 Create 创建一个临时的 JsonObject 包装器
        var tempRawJsonNode = JsonObject.Create(jsonObject);

        // 2. 如果创建成功，就使用 CloneJsonNode 方法来创建深拷贝
        JsonObject finalRawJsonData;
        if (tempRawJsonNode != null && tempRawJsonNode.CloneJsonNode(out var clonedNode) && clonedNode is JsonObject clonedObject)
        {
            finalRawJsonData = clonedObject;
        }
        else
        {
            // 如果克隆失败，则回退到一个空的 JsonObject
            finalRawJsonData = new JsonObject();
        }

        return new UnknownRuneConfig
        {
            Name = name ?? "读取名称失败",
            ConfigId = configId ?? "读取ID失败",
            OriginalRuneType = originalRuneType ?? "未知",
            ErrorMessage = errorMessage,
            RawJsonData = finalRawJsonData,
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AbstractRuneConfig value, JsonSerializerOptions options)
    {
        // 对于序列化，System.Text.Json 在传递实际类型（value.GetType()）时能正确处理。
        // 它会序列化具体类型的所有属性，包括 RuneType 属性。
        // RuneType 属性的名称将根据其在具体类中的定义（通常是 "RuneType"，因为 nameof(AbstractRuneConfig.RuneType) 是 "RuneType"）
        // 以及 JsonSerializerOptions 中的命名策略（如 PropertyNamingPolicy）来确定。
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}