using System.Text.Json;
using System.Text.Json.Serialization;

namespace YAESandBox.Workflow.AIService.AiConfig;

/// <summary>
/// 自定义 JsonConverter 用于处理 AbstractAiProcessorConfig 及其派生类。
/// 它根据 JSON 对象中的 "ConfigType" 属性（忽略大小写）来确定具体的派生类型。
/// </summary>
public class AbstractAiProcessorConfigConverter : JsonConverter<AbstractAiProcessorConfig>
{
    private const string ConfigTypePropertyName = nameof(AbstractAiProcessorConfig.ConfigType);

    /// <summary>
    /// 确定此转换器是否可以转换指定的对象类型。
    /// </summary>
    public override bool CanConvert(Type typeToConvert)
    {
        // 此转换器处理 AbstractAiProcessorConfig 及其所有派生类
        return typeof(AbstractAiProcessorConfig).IsAssignableFrom(typeToConvert);
    }

    /// <summary>
    /// 从 JSON 读取并转换为 AbstractAiProcessorConfig 的派生类型。
    /// </summary>
    public override AbstractAiProcessorConfig? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // 如果是 null token，直接返回 null
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        // 期望一个 JSON 对象的开始
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("JSON 数据应以对象起始符 '{' 开始。");
        }

        // 由于 Utf8JsonReader 是只进的，我们需要先将 JSON 对象解析为 JsonElement
        // 以便能够查找 'ConfigType' 属性，然后再决定具体类型进行反序列化。
        // 注意：这会导致 JSON 被解析两次（一次到 JsonElement，一次到具体类型）。
        // 对于性能敏感场景，可以考虑更复杂的 reader.Read() 循环查找，但会增加代码复杂度。
        using var jsonDocument = JsonDocument.ParseValue(ref reader);
        var jsonObject = jsonDocument.RootElement;

        string? configTypeValue = null;
        bool configTypeFound = false;

        // 遍历 JSON 对象的属性，不区分大小写地查找 "ConfigType"
        foreach (var property in jsonObject.EnumerateObject())
        {
            if (string.Equals(property.Name, ConfigTypePropertyName, StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    configTypeValue = property.Value.GetString();
                }
                else
                {
                    // 如果 ConfigType 存在但不是字符串，则视为错误
                    throw new JsonException($"属性 '{ConfigTypePropertyName}' 的值必须是字符串类型。");
                }

                configTypeFound = true;
                break;
            }
        }

        // 如果没有找到 ConfigType 属性，或者其值为空，则抛出异常
        if (!configTypeFound)
        {
            throw new JsonException(
                $"JSON 对象中缺少必需的属性 '{ConfigTypePropertyName}'，无法确定其具体类型。");
        }

        if (string.IsNullOrWhiteSpace(configTypeValue))
        {
            throw new JsonException($"属性 '{ConfigTypePropertyName}' 的值不能为空或仅包含空白字符。");
        }

        // 使用 ConfigSchemasHelper 获取具体的 AI 配置类型
        var actualTargetType = ConfigSchemasHelper.GetAiConfigTypeByName(configTypeValue);
        if (actualTargetType == null)
        {
            throw new JsonException($"未知或不支持的 '{ConfigTypePropertyName}': '{configTypeValue}'。找不到对应的 .NET 类型。");
        }

        // 检查获取到的类型是否确实是 AbstractAiProcessorConfig 的派生类
        if (!typeof(AbstractAiProcessorConfig).IsAssignableFrom(actualTargetType))
        {
            throw new JsonException(
                $"根据 '{ConfigTypePropertyName}' 的值 '{configTypeValue}' 找到的类型 '{actualTargetType.FullName}' 未继承自 '{nameof(AbstractAiProcessorConfig)}'。");
        }

        // 现在我们有了目标具体类型，可以将原始 JSON (jsonObject.GetRawText())
        // 或者 JsonElement 本身 (jsonObject) 反序列化为该具体类型。
        // 使用 GetRawText() 更能确保 JsonSerializer 从头开始处理。
        // 注意：这里的 options 会被传递下去，如果 options 中包含此转换器，
        // System.Text.Json 内部有机制防止无限递归调用同一个转换器处理同一个对象。
        try
        {
            var result = JsonSerializer.Deserialize(jsonObject.GetRawText(), actualTargetType, options) as AbstractAiProcessorConfig;
            // 理论上，如果 actualTargetType 不是可空类型，Deserialize 不应返回 null，除非 JSON 是 "null"
            // 但由于我们已经处理了 JsonTokenType.Null，且 jsonObject 是一个对象，这里应得到一个实例
            if (result == null && Nullable.GetUnderlyingType(actualTargetType) == null)
            {
                // 这通常不应该发生，除非反序列化逻辑对于特定类型返回了 null
                throw new JsonException($"将 '{configTypeValue}' 反序列化到类型 '{actualTargetType.FullName}' 的结果为 null，但该类型不可为空。");
            }

            return result;
        }
        catch (JsonException ex)
        {
            // 重新抛出，添加更多上下文
            throw new JsonException($"将 JSON 反序列化为配置类型 '{configTypeValue}' 的具体类型 '{actualTargetType.FullName}' 时出错。", ex);
        }
        catch (Exception ex) // 捕获其他可能的反序列化异常
        {
            throw new JsonException(
                $"反序列化到目标类型 '{actualTargetType.FullName}' 时发生意外错误。", ex);
        }
    }

    /// <summary>
    /// 将 AbstractAiProcessorConfig 对象写入 JSON。
    /// </summary>
    public override void Write(
        Utf8JsonWriter writer,
        AbstractAiProcessorConfig value,
        JsonSerializerOptions options)
    {
        // 应当不需要判空
        // if (value == null)
        // {
        //     writer.WriteNullValue();
        //     return;
        // }

        // 获取对象的实际运行时类型，以便序列化所有派生类的属性
        var actualType = value.GetType();

        // 使用 JsonSerializer.Serialize 进行序列化。
        // System.Text.Json 会处理对象的实际类型，
        // 并应用 options 中定义的策略（如命名策略）。
        // 它不会再次调用这个转换器来序列化同一个 'value' 对象，从而避免无限循环。
        JsonSerializer.Serialize(writer, value, actualType, options);
    }
}