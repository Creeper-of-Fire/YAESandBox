using System.Text.Json;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;

namespace YAESandBox.Core.Block;

// 可能还需要一个辅助方法来将 DTO 映射到 Core 类型
internal static class PersistenceMapper
{
    // Helper to convert DTOs back to Core objects
    public static WorldState? MapWorldState(WorldStateDto? wsDto)
    {
        if (wsDto == null) return null;
        var ws = new WorldState();
        MapEntities(ws.Items, wsDto.Items, EntityType.Item);
        MapEntities(ws.Characters, wsDto.Characters, EntityType.Character);
        MapEntities(ws.Places, wsDto.Places, EntityType.Place);
        return ws;
    }

    private static void MapEntities<TEntity>(Dictionary<string, TEntity> targetDict,
        Dictionary<string, EntityDto> sourceDict, EntityType type)
        where TEntity : BaseEntity
    {
        foreach (var kvp in sourceDict)
        {
            var entity = CreateEntityInstance(type, kvp.Key);
            var attributes = kvp.Value.Attributes;

            // ----- Bug 修复：更健壮地恢复 IsDestroyed -----
            if (attributes.TryGetValue("IsDestroyed", out object? isDestroyedVal))
            {
                switch (isDestroyedVal)
                {
                    // 检查是否直接是 bool 类型
                    case bool destroyedBool:
                        entity.IsDestroyed = destroyedBool;
                        break;
                    // 否则，检查是否是 JsonElement 表示的 true
                    case JsonElement { ValueKind: JsonValueKind.True }:
                        entity.IsDestroyed = true;
                        break;
                }
                // 其他情况（比如 null, 或者非 bool/JsonElement.True）都视为 false （保持默认值）
                // entity.IsDestroyed 默认为 false，所以这里不用显式设置 false
            }
            // 如果 "IsDestroyed" 键不存在，entity.IsDestroyed 保持默认的 false

            foreach (var attr in kvp.Value.Attributes.Where(attr => !BaseEntity.CoreFields.Contains(attr.Key)))
            {
                // *** 这里是关键：需要正确恢复 object? 类型 ***
                entity.SetAttribute(attr.Key, DeserializeObjectValue(attr.Value));
            }

            targetDict.Add(kvp.Key, (TEntity)entity);
        }
    }

    // 辅助方法：创建实体实例 (已存在于 Block.cs)
    internal static BaseEntity CreateEntityInstance(EntityType type, string id)
    {
        return type switch
        {
            EntityType.Item => new Item(id),
            EntityType.Character => new Character(id),
            EntityType.Place => new Place(id),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    // 原有方法保持不变，用于处理 object? 类型
    internal static object? DeserializeObjectValue(object? value)
    {
        if (value is not JsonElement element)
            return value; // 如果不是 JsonElement (例如已经是基本类型)，直接返回
        return DeserializeObjectValue(element); // 调用重载方法
    }

    // *** 非常重要：处理反序列化后的 object? 值 ***
    internal static object? DeserializeObjectValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            case JsonValueKind.Number:
                if (element.TryGetInt32(out int i)) return i;
                if (element.TryGetInt64(out long l)) return l; // Handle larger ints if needed
                if (element.TryGetDecimal(out decimal dec)) return dec;
                if (element.TryGetDouble(out double d)) return d; // Or Decimal
                return element.GetRawText(); // Fallback or throw
            case JsonValueKind.String:
                // 尝试识别 TypedID 字符串（如果使用字符串格式）
                // if (TryParseTypedIdString(element.GetString(), out TypedID tid)) return tid;
                return element.GetString();
            case JsonValueKind.Array:
                // 递归处理数组元素
                return element.EnumerateArray().Select(DeserializeObjectValue).ToList();
            case JsonValueKind.Object:
                // // ----- Bug 1 & 3 修复：优先手动检测 TypedID 结构 -----
                // // 检查是否存在 "type" 和 "id" 属性，并且类型是预期的（string 或 number for type, string for id）
                // if (element.TryGetProperty("type", out var typeProp) && // 检查 "type" 属性
                //     element.TryGetProperty("id", out var idProp) &&   // 检查 "id" 属性
                //     idProp.ValueKind == JsonValueKind.String)         // 确保 "id" 是字符串
                // {
                //     string? typeString = null;
                //     switch (typeProp.ValueKind)
                //     {
                //         // EntityType 可能被序列化为字符串（推荐）或数字
                //         case JsonValueKind.String:
                //             typeString = typeProp.GetString();
                //             break;
                //         case JsonValueKind.Number when typeProp.TryGetInt32(out int typeInt):
                //         {
                //             // 如果枚举被序列化为数字，尝试转换
                //             if (Enum.IsDefined(typeof(EntityType), typeInt)) 
                //                 typeString = ((EntityType)typeInt).ToString(); // 转回字符串以便统一处理
                //
                //             break;
                //         }
                //     }
                //
                //     // 使用 Enum.TryParse 进行健壮的解析（忽略大小写）
                //     if (typeString != null && Enum.TryParse<EntityType>(typeString, ignoreCase: true, out var entityType))
                //     {
                //         var idString = idProp.GetString();
                //         if (idString != null) // 再次确认 id 字符串不为 null
                //         {
                //             // 成功识别并解析为 TypedID
                //             return new TypedID(entityType, idString);
                //         }
                //     }
                //     // 如果解析失败，记录日志并继续按普通字典处理
                //     Log.Warning($"JSON 对象结构类似 TypedID，但无法解析 'type' ('{typeString}') 或 'id'。将按普通字典处理。原始JSON: {element.GetRawText()}");
                // }
                // // ----- TypedID 手动检测结束 -----
                try
                {
                    // 注意：需要确保 JsonSerializerOptions 传递正确
                    // 这里的 options 需要包含 TypedIdConverter 和 JsonStringEnumConverter
                    var options = BlockManager._jsonOptions; // 假设 BlockManager 中的 _jsonOptions 可访问或重新创建
                    var typedId = element.Deserialize<TypedID>(options);
                    // 如果反序列化成功（没有抛异常），并且结果不为 null
                    if (typedId != default) // 检查是否为默认值，因为 record struct 不是 null
                    {
                        return typedId;
                    }
                    // 如果反序列化为默认值，可能不是 TypedID 结构，继续按字典处理
                }
                catch (JsonException)
                {
                    // 反序列化失败，说明它不是一个有效的 TypedID JSON，按普通字典处理
                    Log.Debug($"JSON object was not a valid TypedID. Deserializing as dictionary. JSON: {element.GetRawText()}");
                }
                catch (Exception ex) // 其他意外错误
                {
                    Log.Error(ex, $"Unexpected error during potential TypedID deserialization. JSON: {element.GetRawText()}");
                    // 根据情况决定是继续按字典处理还是抛出异常
                }

                // 处理普通字典
                var dict = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = DeserializeObjectValue(prop.Value);
                }

                return dict;
            case JsonValueKind.Null: return null;
            // Undefined, Comment not typically possible here
            case JsonValueKind.Undefined:
            default:
                return element.GetRawText(); // Fallback or throw
        }
    }

    public static GameState MapGameState(Dictionary<string, object?> gsDto)
    {
        var gs = new GameState();
        foreach (var kvp in gsDto)
        {
            // *** 同样需要处理 object? 类型恢复 ***
            gs[kvp.Key] = DeserializeObjectValue(kvp.Value);
        }

        return gs;
    }
}