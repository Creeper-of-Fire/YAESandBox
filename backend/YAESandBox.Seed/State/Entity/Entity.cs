#nullable enable // 启用 nullable 上下文

using System.Text.Json.Serialization;

// For TryGetValue

// For LINQ operations if needed later, like in FindEntityByName

// 定义核心命名空间
namespace YAESandBox.Seed.State.Entity;
// --- 核心类型定义 ---

/// <summary>
/// 代表实体的类型。
/// </summary>
public enum EntityType
{
    Item,
    Character,
    Place
}

/// <summary>
/// 类型化的实体ID，用于精确引用。使用 record struct 实现值相等性。
/// </summary>
[JsonConverter(typeof(TypedIdConverter))]
public readonly record struct TypedId(EntityType Type, string Id)
{
    // 提供一个方便的字符串表示，用于日志或调试
    public override string ToString() => $"{this.Type}:{this.Id}";
}

// --- WorldState 定义 ---

// --- 核心数据模型 ---

// --- 子类定义 ---

public class Item(string entityId) : BaseEntity(entityId)
{
    public override EntityType EntityType =>
        EntityType.Item;

    public override void SetAttribute(string key, object? value)
    {
        if (key.Equals("quantity"))
            base.SetAttribute(key, value is int quantityValue and >= 0 ? quantityValue : 0);
        else
            base.SetAttribute(key, value);
    }
}

public class Character(string entityId) : BaseEntity(entityId)
{
    public override EntityType EntityType =>
        EntityType.Character;
}

public class Place(string entityId) : BaseEntity(entityId)
{
    public override EntityType EntityType =>
        EntityType.Place;
}