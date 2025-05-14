using System.Diagnostics.CodeAnalysis;
using YAESandBox.Depend;

namespace YAESandBox.Core.State.Entity;

/// <summary>
/// 所有游戏实体的基类。
/// </summary>
public abstract class BaseEntity
{
    // 使用 static readonly 定义核心字段集合
    public static readonly HashSet<string> CoreFields = [nameof(EntityId), nameof(EntityType), nameof(IsDestroyed)];

    /// <summary>
    /// 实体的唯一 ID。
    /// </summary>
    public string EntityId { get; init; } // 使用 init 使其在构造后不可变

    /// <summary>
    /// 实体的类型。子类应覆盖此属性。
    /// </summary>
    public abstract EntityType EntityType { get; }

    /// <summary>
    /// 实体是否已被销毁。
    /// </summary>
    public bool IsDestroyed { get; set; } = false;

    // 用于存储动态添加的属性
    private readonly Dictionary<string, object?> _dynamicAttributes = new();

    /// <summary>
    /// 获取实体的 TypedID 表示。
    /// </summary>
    public TypedId TypedId =>
        new(this.EntityType, this.EntityId);

    // 主构造函数，确保核心属性被初始化
    protected BaseEntity(string entityId)
    {
        // 添加基本的 ID 验证
        if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentException("Entity ID cannot be null or whitespace.", nameof(entityId));

        this.EntityId = entityId;
    }

    /// <summary>
    /// 获取属性值。先检查核心属性，再检查动态属性。
    /// </summary>
    /// <param name="key">属性键。</param>
    /// <param name="defaultValue">如果属性不存在时返回的默认值。</param>
    /// <returns>属性值或默认值。</returns>
    public object? GetAttribute(string key, object? defaultValue = null)
    {
        // 使用 switch 表达式提高可读性
        return key switch
        {
            nameof(this.EntityId) => this.EntityId,
            nameof(this.EntityType) => this.EntityType, // 注意这访问的是属性
            nameof(this.IsDestroyed) => this.IsDestroyed,
            _ => this._dynamicAttributes.GetValueOrDefault(key, defaultValue)
        };
    }

    /// <summary>
    /// 尝试获取指定类型的属性值。
    /// </summary>
    /// <typeparam name="T">期望的属性值类型。</typeparam>
    /// <param name="key">属性键。</param>
    /// <param name="value">如果成功获取并转换，则输出属性值。</param>
    /// <returns>如果属性存在且类型匹配，则为 true；否则为 false。</returns>
    public bool TryGetAttribute<T>(string key, [MaybeNullWhen(false)] out T value)
    {
        object? rawValue = this.GetAttribute(key);
        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        // 特殊处理：如果期望 TypedID? 而存储的是 TypedID
        if (typeof(T) == typeof(TypedId?) && rawValue is TypedId typedIdValue)
        {
            value = (T)(object)typedIdValue; // 需要强制转换
            return true;
        }

        // 特殊处理：如果期望 List<TypedID> 而存储的是 List<object> 或其他兼容列表
        if (typeof(T) == typeof(List<TypedId>) && rawValue is List<object> objectList)
            try
            {
                // 尝试将 object 列表转换为 TypedID 列表
                var typedIdList = objectList.Cast<TypedId>().ToList();
                value = (T)(object)typedIdList;
                return true;
            }
            catch (InvalidCastException)
            {
                /* 转换失败，忽略 */
            }
        // ... 可以根据需要添加更多类型转换逻辑

        value = default;
        return false;
    }


    /// <summary>
    /// 检查是否存在指定属性（核心或动态）。
    /// </summary>
    public bool HasAttribute(string key)
    {
        return CoreFields.Contains(key) || this._dynamicAttributes.ContainsKey(key);
    }

    /// <summary>
    /// 删除动态属性。不能删除核心属性。
    /// </summary>
    /// <param name="key">要删除的动态属性的键。</param>
    /// <returns>如果成功删除则为 true，如果属性是核心属性或不存在则为 false。</returns>
    public bool DeleteAttribute(string key)
    {
        if (CoreFields.Contains(key))
        {
            Log.Warning($"尝试删除核心属性 '{key}' (实体: {this.TypedId})，已忽略。");
            return false;
        }

        bool removed = this._dynamicAttributes.Remove(key);
        if (removed)
            Log.Debug($"实体 '{this.TypedId}': 删除动态属性 '{key}'。");
        else
            Log.Debug($"实体 '{this.TypedId}': 尝试删除不存在的动态属性 '{key}'。");

        return removed;
    }

    /// <summary>
    /// 获取所有属性（核心和动态）的字典表示。
    /// </summary>
    /// <returns>包含所有属性的字典。</returns>
    public Dictionary<string, object?> GetAllAttributes()
    {
        // 使用 LINQ 和字典初始化器更简洁
        var allAttrs = new Dictionary<string, object?>
        {
            [nameof(this.EntityId)] = this.EntityId,
            [nameof(this.EntityType)] = this.EntityType,
            [nameof(this.IsDestroyed)] = this.IsDestroyed
        }; // 考虑是否需要忽略大小写

        // 合并动态属性
        foreach (var kvp in this._dynamicAttributes)
            // 简单的浅拷贝处理（对于 List 和 Dictionary）
            // 注意：这不会深拷贝嵌套的复杂对象
            allAttrs[kvp.Key] = kvp.Value switch
            {
                List<object> list => new List<object>(list), // 拷贝列表
                Dictionary<string, object> dict => new Dictionary<string, object>(dict), // 拷贝字典
                _ => kvp.Value // 其他类型直接赋值
            };

        return allAttrs;
    }


    /// <summary>
    /// 设置属性值。不执行引用检查或关系维护（已在 Python 代码中移除）。
    /// 由调用者（通常是原子 API 端点或其内部逻辑）确保值的类型和有效性。
    /// 子类可以覆盖此方法进行特定验证。
    /// </summary>
    /// <param name="key">属性键。</param>
    /// <param name="value">新的属性值。</param>
    /// <exception cref="ArgumentException">如果尝试设置只读核心属性或类型验证失败。</exception>
    public virtual void SetAttribute(string key, object? value)
    {
        // logger.LogDebug($"实体 '{this.TypedId}': (Core) SetAttribute: Key='{key}', Value='{value?.ToString() ?? "null"}'");

        switch (key)
        {
            // 处理核心属性
            case nameof(this.EntityId):
                Log.Warning($"核心属性 '{nameof(this.EntityId)}' 是只读的。");
                break;
            case nameof(this.EntityType):
                Log.Warning($"核心属性 '{nameof(this.EntityType)}' 是只读的。");
                break;
            case nameof(this.IsDestroyed) when value is bool isDestroyedValue:
                this.IsDestroyed = isDestroyedValue;
                // logger.LogDebug($"实体 '{this.TypedId}': 设置核心属性 {nameof(IsDestroyed)} = {this.IsDestroyed}");
                return; // 设置完成
            case nameof(this.IsDestroyed):
                Log.Warning($"核心属性 '{nameof(this.IsDestroyed)}' 需要布尔类型值，得到 {value?.GetType().Name ?? "null"}");
                break;
        }

        // 设置动态属性
        // 子类覆盖时应先调用 base.SetAttribute 或在这里进行特定验证
        this._dynamicAttributes[key] = value;
        // logger.LogDebug($"实体 '{this.TypedId}': 设置动态属性 {key} = {value?.ToString() ?? "null"}");
    }

    public void ModifyAttribute(string key, string stringOp, object value)
    {
        this.ModifyAttribute(key, OperatorHelper.StringToOperator(stringOp), value);
    }


    /// <summary>
    /// 修改属性值。基类处理数值、字符串、列表和字典的通用操作。
    /// 不执行引用检查或关系维护。
    /// </summary>
    /// <param name="key">属性键。</param>
    /// <param name="op">操作符。</param>
    /// <param name="value">操作符和值</param>
    public virtual void ModifyAttribute(string key, Operator op, object value)
    {
        // logger.LogDebug($"实体 '{this.TypedId}': (Core) ModifyAttribute: Key='{key}', Op='{op}', Value='{value?.ToString() ?? "null"}'");

        // 核心属性处理 (仅支持 '=')
        if (CoreFields.Contains(key))
        {
            if (op == Operator.Equal)
                // 调用 SetAttribute 处理核心属性赋值和验证
                this.SetAttribute(key, value);
            else
                Log.Warning($"核心属性 '{key}' 不支持操作符 '{op}'");
            return;
        }

        // 获取当前动态属性值
        this._dynamicAttributes.TryGetValue(key, out object? currentValue);
        object newValueToSet = op.ChangedValue(currentValue, value);
        this.SetAttribute(key, newValueToSet);
    }

    /// <summary>
    /// 创建此实体的深拷贝副本。
    /// 子类应覆盖此方法以确保所有特定字段都被正确复制。
    /// </summary>
    /// <returns>一个新的实体实例，包含当前所有属性的深拷贝。</returns>
    public virtual BaseEntity Clone()
    {
        // 使用反射或手动创建新实例可能不是最高效的，但对于原型是可行的
        // 一个更健壮的方法是为每个子类实现具体的 Clone 方法

        // 1. 创建子类的新实例 (这里需要一种方式来实例化正确的子类型)
        BaseEntity clone;
        switch (this.EntityType)
        {
            case EntityType.Item: clone = new Item(this.EntityId); break;
            case EntityType.Character: clone = new Character(this.EntityId); break;
            case EntityType.Place: clone = new Place(this.EntityId); break;
            default: throw new InvalidOperationException($"无法克隆未知的实体类型: {this.EntityType}");
        }

        // 2. 复制核心属性 (IsDestroyed 是唯一可变的)
        clone.IsDestroyed = this.IsDestroyed;

        // 3. 深拷贝动态属性
        foreach (var kvp in this._dynamicAttributes)
            // 实现基本的深拷贝逻辑 (对 List 和 Dictionary)
            // 注意: 这不会递归深拷贝复杂对象内部的引用
            clone._dynamicAttributes[kvp.Key] = kvp.Value switch
            {
                List<object> list => new List<object>(list), // 拷贝列表本身，内部元素是浅拷贝
                Dictionary<string, object> dict => new Dictionary<string, object>(dict), // 拷贝字典本身，内部值是浅拷贝
                _ => kvp.Value // 其他类型直接赋值 (浅拷贝)
            };

        return clone;
    }
}