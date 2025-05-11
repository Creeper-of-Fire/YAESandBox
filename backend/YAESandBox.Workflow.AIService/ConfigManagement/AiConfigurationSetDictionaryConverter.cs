using System.Text.Json;
using System.Text.Json.Serialization;
using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.AIService.AiConfigSchema;

namespace YAESandBox.Workflow.AIService.ConfigManagement;

public class AiConfigurationSetDictionaryConverter : JsonConverter<Dictionary<string, AbstractAiProcessorConfig>>
{
    public override Dictionary<string, AbstractAiProcessorConfig> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token for Dictionary<string, AbstractAiProcessorConfig>");
        }

        var dictionary = new Dictionary<string, AbstractAiProcessorConfig>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token.");
            }

            string moduleTypeKey = reader.GetString() ?? throw new JsonException("Dictionary key (ModuleType) cannot be null.");
            reader.Read(); // Move to the value

            // 根据 moduleTypeKey 动态确定要反序列化的具体类型
            var concreteConfigType = ConfigSchemasHelper.GetTypeByName(moduleTypeKey);
            if (concreteConfigType == null || !typeof(AbstractAiProcessorConfig).IsAssignableFrom(concreteConfigType))
            {
                // 策略：是抛出异常，还是跳过未知的模块类型，还是尝试作为基类（如果允许）？
                // 为了健壮性，如果找不到匹配的类型，可以记录警告并跳过，或者抛出更具体的异常。
                // 这里我们先抛异常，因为如果 key 存在，值应该是有效的。
                throw new JsonException(
                    $"Unknown or invalid ModuleType '{moduleTypeKey}' found as dictionary key. Cannot determine concrete type for AbstractAiProcessorConfig.");
            }

            // 使用 JsonSerializer.Deserialize 来反序列化值对象为具体的类型
            // 注意：这里的 options 需要是不包含此转换器的 options，以避免无限递归。
            // 通常，JsonSerializer 内部会处理好这一点，但如果遇到问题，可能需要创建一个新的 options 实例。
            var configValue = (AbstractAiProcessorConfig?)JsonSerializer.Deserialize(ref reader, concreteConfigType, options);

            if (configValue == null)
            {
                throw new JsonException($"Failed to deserialize value for ModuleType '{moduleTypeKey}'.");
            }

            // （可选）如果 AbstractAiProcessorConfig 仍然有一个 ModuleType 属性，
            // 可以在这里手动设置它，使其与字典的键一致。
            // 例如： if (configValue is IModuleTypedConfig typedConfig) { typedConfig.ModuleType = moduleTypeKey; }
            // 但如果目标是完全不在对象内部存储 ModuleType，则跳过此步。

            dictionary.Add(moduleTypeKey, configValue);
        }

        throw new JsonException("Unexpected end of JSON when reading Dictionary.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<string, AbstractAiProcessorConfig> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            // Key 是 ModuleType 字符串
            writer.WritePropertyName(kvp.Key);

            // Value 是 AbstractAiProcessorConfig 的具体实例
            // 当序列化 kvp.Value 时，如果 AbstractAiProcessorConfig (或其子类)
            // 内部仍然定义了 ModuleType 属性，并且没有 [JsonIgnore]，它会被序列化出来。
            // 为了不在值对象中序列化出 ModuleType，你需要确保：
            // 1. AbstractAiProcessorConfig 及其子类没有 ModuleType 属性。
            // OR
            // 2. ModuleType 属性上有 [JsonIgnore(Condition = JsonIgnoreCondition.Always)] (或类似)。
            // OR
            // 3. 在这里的 options 中提供一个修改器，临时移除该属性的序列化。 (更复杂)

            // 假设 AbstractAiProcessorConfig 及其子类不再有 ModuleType 属性，
            // 或者它被配置为不序列化。
            JsonSerializer.Serialize(writer, kvp.Value, kvp.Value.GetType(), options); // 重要：使用 GetType() 序列化为具体类型
        }

        writer.WriteEndObject();
    }
}