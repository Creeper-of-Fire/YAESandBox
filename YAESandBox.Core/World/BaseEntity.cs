using System.Diagnostics.CodeAnalysis;

namespace YAESandBox.Core.World;

/// <summary>
/// 所有游戏实体的基类。
/// </summary>
public abstract class BaseEntity
{
    // 使用 static readonly 定义核心字段集合
    protected static readonly HashSet<string> CoreFields = new()
    {
        nameof(EntityId), nameof(EntityType), nameof(IsDestroyed)
        // 注意：这里使用 nameof 来避免硬编码字符串，提高重构安全性
    };

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
    public TypedID TypedId =>
        new TypedID(this.EntityType, this.EntityId);

    // 主构造函数，确保核心属性被初始化
    protected BaseEntity(string entityId)
    {
        // 添加基本的 ID 验证
        if (string.IsNullOrWhiteSpace(entityId))
        {
            throw new ArgumentException("Entity ID cannot be null or whitespace.", nameof(entityId));
        }

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
        if (typeof(T) == typeof(TypedID?) && rawValue is TypedID typedIdValue)
        {
            value = (T)(object)typedIdValue; // 需要强制转换
            return true;
        }

        // 特殊处理：如果期望 List<TypedID> 而存储的是 List<object> 或其他兼容列表
        if (typeof(T) == typeof(List<TypedID>) && rawValue is List<object> objectList)
        {
            try
            {
                // 尝试将 object 列表转换为 TypedID 列表
                var typedIdList = objectList.Cast<TypedID>().ToList();
                value = (T)(object)typedIdList;
                return true;
            }
            catch (InvalidCastException)
            {
                /* 转换失败，忽略 */
            }
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
            // logger.LogWarning($"尝试删除核心属性 '{key}' (实体: {this.TypedId})，已忽略。");
            return false;
        }

        bool removed = this._dynamicAttributes.Remove(key);
        if (removed)
        {
            // logger.LogDebug($"实体 '{this.TypedId}': 删除动态属性 '{key}'。");
        }
        else
        {
            // logger.LogDebug($"实体 '{this.TypedId}': 尝试删除不存在的动态属性 '{key}'。");
        }

        return removed;
    }

    /// <summary>
    /// 获取所有属性（核心和动态）的字典表示。
    /// </summary>
    /// <returns>包含所有属性的字典。</returns>
    public Dictionary<string, object?> GetAllAttributes()
    {
        // 使用 LINQ 和字典初始化器更简洁
        var allAttrs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(this.EntityId)] = this.EntityId,
            [nameof(this.EntityType)] = this.EntityType,
            [nameof(this.IsDestroyed)] = this.IsDestroyed
        }; // 考虑是否需要忽略大小写

        // 合并动态属性
        foreach (var kvp in this._dynamicAttributes)
        {
            // 简单的浅拷贝处理（对于 List 和 Dictionary）
            // 注意：这不会深拷贝嵌套的复杂对象
            allAttrs[kvp.Key] = kvp.Value switch
            {
                List<object> list => new List<object>(list), // 拷贝列表
                Dictionary<string, object> dict => new Dictionary<string, object>(dict), // 拷贝字典
                _ => kvp.Value // 其他类型直接赋值
            };
        }

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

        // 处理核心属性
        if (key.Equals(nameof(this.EntityId), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"核心属性 '{nameof(this.EntityId)}' 是只读的。", nameof(key));
        }

        if (key.Equals(nameof(this.EntityType), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"核心属性 '{nameof(this.EntityType)}' 是只读的。", nameof(key));
        }

        if (key.Equals(nameof(this.IsDestroyed), StringComparison.OrdinalIgnoreCase))
        {
            if (value is bool isDestroyedValue)
            {
                this.IsDestroyed = isDestroyedValue;
                // logger.LogDebug($"实体 '{this.TypedId}': 设置核心属性 {nameof(IsDestroyed)} = {this.IsDestroyed}");
                return; // 设置完成
            }

            throw new ArgumentException(
                $"核心属性 '{nameof(this.IsDestroyed)}' 需要布尔类型值，得到 {value?.GetType().Name ?? "null"}", nameof(value));
        }

        // 设置动态属性
        // 子类覆盖时应先调用 base.SetAttribute 或在这里进行特定验证
        this._dynamicAttributes[key] = value;
        // logger.LogDebug($"实体 '{this.TypedId}': 设置动态属性 {key} = {value?.ToString() ?? "null"}");
    }

    public void ModifyAttribute(string key, (string stringOp, object? value) opAndValue)
    {
        ModifyAttribute(key, OperatorSever.StringToOperator(stringOp: stringOp));
    }


    /// <summary>
    /// 修改属性值。基类处理数值、字符串、列表和字典的通用操作。
    /// 不执行引用检查或关系维护。
    /// </summary>
    /// <param name="key">属性键。</param>
    /// <param name="opAndValue">包含操作符 ('=', '+=', '-=') 和值的元组。</param>
    /// <exception cref="ArgumentException">如果操作符或值类型无效。</exception>
    /// <exception cref="InvalidOperationException">如果尝试对核心属性执行不支持的操作。</exception>
    public virtual void ModifyAttribute(string key, Operator op, object? value)
    {
        // logger.LogDebug($"实体 '{this.TypedId}': (Core) ModifyAttribute: Key='{key}', Op='{op}', Value='{value?.ToString() ?? "null"}'");

        // 核心属性处理 (仅支持 '=')
        if (CoreFields.Contains(key))
        {
            if (op == Operator.Equal)
            {
                // 调用 SetAttribute 处理核心属性赋值和验证
                this.SetAttribute(key, value);
            }
            else
            {
                throw new InvalidOperationException(
                    $"核心属性 '{key}' (类型: {this.GetType().GetProperty(key)?.PropertyType.Name ?? "未知"}) 不支持操作符 '{op}'");
            }

            return;
        }

        // 获取当前动态属性值
        this._dynamicAttributes.TryGetValue(key, out object? currentValue);
        bool wasModified = false;
        object? newValueToSet = null;

        // --- 处理各种操作 ---

        if (op == Operator.Equal)
        {
            this.SetAttribute(key, value);
            return;
        }

        if (op == Operator.Add) // 加/合并 (+= / +)
        {
            if (currentValue == null) // 属性不存在，视为首次设置
            {
                // logger.LogDebug($"Modify '{key}' {op} {value}: 当前值为 null，视为首次设置。");
                newValueToSet = value; // 直接使用新值
                wasModified = true;
            }
            else if (currentValue is int currentInt && value is int intValue)
            {
                newValueToSet = currentInt + intValue;
                wasModified = newValueToSet is not null && !newValueToSet.Equals(currentValue);
            }
            else if (currentValue is double currentDouble && value is double doubleValue) // 支持 double
            {
                newValueToSet = currentDouble + doubleValue;
                wasModified = newValueToSet is not null && !newValueToSet.Equals(currentValue);
            }
            else if (currentValue is float currentFloat && value is float floatValue) // 支持 float
            {
                newValueToSet = currentFloat + floatValue;
                wasModified = newValueToSet is not null && !newValueToSet.Equals(currentValue);
            }
            else if (currentValue is decimal currentDecimal && value is decimal decimalValue) // 支持 decimal
            {
                newValueToSet = currentDecimal + decimalValue;
                wasModified = newValueToSet is not null && !newValueToSet.Equals(currentValue);
            }
            else if (currentValue is string currentString && value is string stringValue)
            {
                newValueToSet = currentString + stringValue;
                wasModified = newValueToSet is not null && !newValueToSet.Equals(currentValue);
            }
            else if (currentValue is List<object?> currentList) // 处理 List<object?>
            {
                var itemsToAdd = value switch
                {
                    List<object?> list => list, // 如果值是列表，直接使用
                    IEnumerable<object?> enumerable => enumerable.ToList(), // 如果是其他可枚举，转为列表
                    _ => new List<object?> { value } // 否则视为单个元素
                };

                List<object?> newList = new List<object?>(currentList); // 创建副本
                int initialCount = newList.Count;
                foreach (object? item in itemsToAdd)
                {
                    // 对于引用类型和 record struct，Contains 使用 Equals 比较
                    if (!newList.Contains(item))
                    {
                        newList.Add(item);
                    }
                }

                if (newList.Count != initialCount || !newList.SequenceEqual(currentList)) // 检查是否实际修改
                {
                    newValueToSet = newList;
                    wasModified = true;
                }
            }
            else if (currentValue is Dictionary<string, object?> currentDict) // 处理字典
            {
                if (value is Dictionary<string, object?> dictToAdd)
                {
                    var newDict = new Dictionary<string, object?>(currentDict);
                    bool dictModified = false;
                    foreach (var kvp in dictToAdd)
                    {
                        if (newDict.TryGetValue(kvp.Key, out object? existingValue) &&
                            Equals(existingValue, kvp.Value))
                            continue;
                        newDict[kvp.Key] = kvp.Value;
                        dictModified = true;
                    }

                    if (dictModified)
                    {
                        newValueToSet = newDict;
                        wasModified = true;
                    }
                }
                else
                {
                    throw new ArgumentException(
                        $"字典属性 '{key}' 的 '{op}' 操作需要字典类型的值，得到 {value?.GetType().Name ?? "null"}", nameof(value));
                }
            }
            else
            {
                throw new ArgumentException($"类型 {currentValue.GetType().Name} 不支持 '{op}' 操作 for key '{key}'");
            }
        }
        // 减/移除 (-= / -)
        else if (op == Operator.Sub)
        {
            if (currentValue == null)
            {
                // logger.LogWarning($"Modify({this.TypedId}): 尝试对不存在的属性 '{key}' 执行 '{op}' 操作，已忽略。");
                return;
            }
            else if (currentValue is int currentInt && value is int intValue)
            {
                newValueToSet = currentInt - intValue;
                wasModified = newValueToSet is not null && !newValueToSet.Equals(currentValue);
            }
            else if (currentValue is double currentDouble && value is double doubleValue)
            {
                newValueToSet = currentDouble - doubleValue;
                wasModified = newValueToSet is not null && !newValueToSet.Equals(currentValue);
            }
            else if (currentValue is float currentFloat && value is float floatValue)
            {
                newValueToSet = currentFloat - floatValue;
                wasModified = newValueToSet is not null && !newValueToSet.Equals(currentValue);
            }
            else if (currentValue is decimal currentDecimal && value is decimal decimalValue)
            {
                newValueToSet = currentDecimal - decimalValue;
                wasModified = newValueToSet is not null && !newValueToSet.Equals(currentValue);
            }
            // 字符串不支持 -=
            else if (currentValue is List<object?> currentList)
            {
                var itemsToRemove = value switch
                {
                    List<object?> list => list,
                    IEnumerable<object?> enumerable => enumerable.ToList(),
                    _ => new List<object?> { value }
                };

                List<object?> newList = new List<object?>(currentList);
                int initialCount = newList.Count;
                // 使用 RemoveAll 提高效率
                newList.RemoveAll(item => itemsToRemove.Contains(item));

                if (newList.Count != initialCount)
                {
                    newValueToSet = newList;
                    wasModified = true;
                }
            }
            else if (currentValue is Dictionary<string, object?> currentDict)
            {
                List<string>? keysToRemove = null;
                if (value is string singleKey)
                {
                    keysToRemove = new List<string> { singleKey };
                }
                else if (value is IEnumerable<string> keyList) // 支持字符串列表
                {
                    keysToRemove = keyList.ToList();
                }
                else
                {
                    throw new ArgumentException(
                        $"字典属性 '{key}' 的 '{op}' 操作需要字符串键或键列表，得到 {value?.GetType().Name ?? "null"}", nameof(value));
                }

                var newDict = new Dictionary<string, object?>(currentDict);
                int initialCount = newDict.Count;
                foreach (string k in keysToRemove)
                {
                    newDict.Remove(k);
                }

                if (newDict.Count != initialCount)
                {
                    newValueToSet = newDict;
                    wasModified = true;
                }
            }
            else
            {
                throw new ArgumentException($"类型 {currentValue.GetType().Name} 不支持 '{op}' 操作 for key '{key}'");
            }
        }
        else
        {
            throw new ArgumentException($"未知的修改操作符 '{op}' for key '{key}'", nameof(opAndValue.op));
        }

        // 如果发生修改，则通过 SetAttribute 写回
        if (wasModified)
        {
            // logger.LogDebug($"实体 '{this.TypedId}': 属性 '{key}' 已通过 '{op}' 修改，调用 SetAttribute 写回。");
            this.SetAttribute(key, newValueToSet); // SetAttribute 会处理动态属性的存储
        }
        else
        {
            // logger.LogDebug($"实体 '{this.TypedId}': 属性 '{key}' 未发生实际修改 (op='{op}')。");
        }
    }

    public void ModifyAttribute(string key, Operator op, int value)
    {
    }
}