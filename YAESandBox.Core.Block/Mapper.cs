using System.Text.Json;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;

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
            var entity = CreateEntityInstance(type, kvp.Key); // Use existing helper
            entity.IsDestroyed = kvp.Value.Attributes.TryGetValue("IsDestroyed", out object? isDestroyedVal) &&
                                 isDestroyedVal is true; // Restore core prop

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
                // 尝试识别 TypedID 对象（如果使用对象格式）
                try
                {
                    // 尝试反序列化为 TypedIdDto，然后转为 TypedID
                    // 这里需要 JsonSerializerOptions，可能需要传递进来或全局获取
                    var dto = element.Deserialize<TypedIdDto>( /* options */);
                    if (dto != null) return dto.ToTypedID();
                }
                catch (JsonException)
                {
                    /* 不是 TypedIdDto，忽略 */
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