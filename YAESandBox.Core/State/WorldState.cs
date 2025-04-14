using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;

namespace YAESandBox.Core.State;

/// <summary>
/// 包含游戏中所有实体状态的核心容器。
/// </summary>
public class WorldState
{
    // 使用字典存储实体，按类型分开
    public Dictionary<string, Item> Items { get; init; } = new();
    public Dictionary<string, Character> Characters { get; init; } = new();
    public Dictionary<string, Place> Places { get; init; } = new();


    /// <summary>
    /// 根据实体类型获取对应的存储字典 (辅助方法)。
    /// 使用泛型和字典映射，避免重复的 if/else。
    /// </summary>
    /// <typeparam name="TEntity">期望的实体类型 (Item, Character, Place)</typeparam>
    /// <returns>对应的实体字典。</returns>
    /// <exception cref="ArgumentException">如果实体类型无效。</exception>
    private IDictionary<string, TEntity> GetEntityDictionary<TEntity>() where TEntity : BaseEntity
    {
        if (typeof(TEntity) == typeof(Item))
            return (IDictionary<string, TEntity>)this.Items;
        if (typeof(TEntity) == typeof(Character))
            return (IDictionary<string, TEntity>)this.Characters;
        if (typeof(TEntity) == typeof(Place))
            return (IDictionary<string, TEntity>)this.Places;

        throw new ArgumentException($"不支持的实体类型: {typeof(TEntity).Name}", nameof(TEntity));
    }

    /// <summary>
    /// 根据实体类型字符串获取对应的非泛型存储字典 (用于内部逻辑)。
    /// </summary>
    private IDictionary<string, BaseEntity> GetEntityDictionary(EntityType entityType)
    {
        switch (entityType)
        {
            case EntityType.Item:
                // 需要显式转换字典值为 BaseEntity
                return this.Items.ToDictionary(kvp => kvp.Key, BaseEntity (kvp) => kvp.Value);
            // 或直接返回 Items 再强制转换，但这样更安全点
            case EntityType.Character:
                return this.Characters.ToDictionary(kvp => kvp.Key, BaseEntity (kvp) => kvp.Value);
            case EntityType.Place:
                return this.Places.ToDictionary(kvp => kvp.Key, BaseEntity (kvp) => kvp.Value);
            default:
                throw new ArgumentOutOfRangeException(nameof(entityType), $"未知的实体类型: {entityType}");
        }
    }

    /// <summary>
    /// 通过 TypedID 精确查找实体。
    /// </summary>
    /// <param name="ref">要查找的实体的 TypedID。</param>
    /// <param name="includeDestroyed">是否包含已标记为销毁的实体。</param>
    /// <returns>找到的实体，如果不存在或不满足条件则返回 null。</returns>
    public BaseEntity? FindEntity(TypedID @ref, bool includeDestroyed = false)
    {
        BaseEntity? entity = null;
        bool found = false;

        // 根据类型查找对应的字典
        switch (@ref.Type)
        {
            case EntityType.Item:
                found = this.Items.TryGetValue(@ref.Id, out var item);
                entity = item;
                break;
            case EntityType.Character:
                found = this.Characters.TryGetValue(@ref.Id, out var character);
                entity = character;
                break;
            case EntityType.Place:
                found = this.Places.TryGetValue(@ref.Id, out var place);
                entity = place;
                break;
            default:
                //理论上 TypedID 枚举会阻止这种情况
                throw new ArgumentOutOfRangeException(nameof(@ref.Type), $"未知的实体类型: {@ref.Type}");
        }

        if (found && entity != null && (!entity.IsDestroyed || includeDestroyed))
        {
            return entity;
        }

        return null;
    }

    /// <summary>
    /// 通过 ID 和 类型 精确查找实体。(兼容旧接口)
    /// </summary>
    public BaseEntity? FindEntityById(string entityId, EntityType entityType, bool includeDestroyed = false)
    {
        return this.FindEntity(new TypedID(entityType, entityId), includeDestroyed);
    }

    /// <summary>
    /// 添加实体到世界状态，仅在同类型中检查 ID 冲突。
    /// </summary>
    /// <param name="entity">要添加的实体。</param>
    /// <exception cref="ArgumentNullException">如果 entity 为 null。</exception>
    /// <exception cref="ArgumentException">如果实体类型无效。</exception>
    public void AddEntity(BaseEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        Log.Debug($"尝试添加实体: {entity.TypedId}");

        switch (entity)
        {
            case Item item:
                if (this.Items.TryGetValue(item.EntityId, out var existingItem) && !existingItem.IsDestroyed)
                {
                    Log.Warning($"覆盖已存在且未销毁的 Item: {item.TypedId}");
                }

                this.Items[item.EntityId] = item;
                break;
            case Character character:
                if (this.Characters.TryGetValue(character.EntityId, out var existingChar) && !existingChar.IsDestroyed)
                {
                    Log.Warning($"覆盖已存在且未销毁的 Character: {character.TypedId}");
                }

                this.Characters[character.EntityId] = character;
                break;
            case Place place:
                if (this.Places.TryGetValue(place.EntityId, out var existingPlace) && !existingPlace.IsDestroyed)
                {
                    Log.Warning($"覆盖已存在且未销毁的 Place: {place.TypedId}");
                }

                this.Places[place.EntityId] = place;
                break;
            default:
                // 如果 BaseEntity 是密封的或者所有子类都已处理，这理论上不应该发生
                throw new ArgumentException($"未知的实体类型实例: {entity.GetType().Name}", nameof(entity));
        }

        Log.Debug($"实体 '{entity.TypedId}' 已添加到 WorldState。");
    }
    
    /// <summary>
    /// 创建 WorldState 的深拷贝副本。
    /// 这将克隆所有的实体及其属性。
    /// </summary>
    /// <returns>一个新的 WorldState 实例。</returns>
    public WorldState Clone()
    {
        var clone = new WorldState();

        // 克隆 Items 字典中的每个 Item
        foreach (var kvp in this.Items)
        {
            // 假设 BaseEntity.Clone() 实现了正确的深拷贝
            clone.Items[kvp.Key] = (Item)kvp.Value.Clone();
        }

        // 克隆 Characters
        foreach (var kvp in this.Characters)
        {
            clone.Characters[kvp.Key] = (Character)kvp.Value.Clone();
        }

        // 克隆 Places
        foreach (var kvp in this.Places)
        {
            clone.Places[kvp.Key] = (Place)kvp.Value.Clone();
        }

        return clone;
    }
}