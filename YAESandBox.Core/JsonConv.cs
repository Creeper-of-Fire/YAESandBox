﻿using System.Text.Json;
using System.Text.Json.Serialization;
using YAESandBox.Core.State.Entity;

namespace YAESandBox.Core;

public class TypedIdConverter : JsonConverter<TypedID>
{
    public override TypedID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token for TypedID");
        }

        EntityType type = default;
        string id = null!;
        bool typeRead = false;
        bool idRead = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (!typeRead || !idRead) throw new JsonException("TypedID object missing 'type' or 'id' property.");
                return new TypedID(type, id);
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string? propertyName = reader.GetString();
                reader.Read(); // Move to property value

                switch (propertyName?.ToLowerInvariant()) // Use case-insensitive matching
                {
                    case "type":
                        // Use EnumConverter to handle string enums robustly
                        var enumConverter = (JsonConverter<EntityType>)options.GetConverter(typeof(EntityType));
                        type = enumConverter.Read(ref reader, typeof(EntityType), options);
                        typeRead = true;
                        break;
                    case "id":
                        id = reader.GetString() ?? throw new JsonException("TypedID 'id' property cannot be null.");
                        idRead = true;
                        break;
                    // You might want to handle unexpected properties, e.g., skip them or throw
                }
            }
        }
        throw new JsonException("Unexpected end when reading TypedID.");
    }

    public override void Write(Utf8JsonWriter writer, TypedID value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        // Use EnumConverter to write string enums
        var enumConverter = (JsonConverter<EntityType>)options.GetConverter(typeof(EntityType));
        writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName("Type") ?? "type"); // Handle naming policy
        enumConverter.Write(writer, value.Type, options);

        writer.WriteString(options.PropertyNamingPolicy?.ConvertName("Id") ?? "id", value.Id);
        writer.WriteEndObject();
    }
}

// 在 Program.cs 或配置 JsonSerializerOptions 的地方注册转换器：
// options.JsonSerializerOptions.Converters.Add(new TypedIdConverter());
// options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); // 确保这个也在