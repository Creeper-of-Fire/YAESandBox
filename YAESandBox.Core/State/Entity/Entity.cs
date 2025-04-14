#nullable enable // 启用 nullable 上下文

using System.Collections;
using System.Diagnostics;
using System.Numerics;
using YAESandBox.Depend;
// For TryGetValue
using ArgumentException = System.ArgumentException; // For LINQ operations if needed later, like in FindEntityByName

// 定义核心命名空间
namespace YAESandBox.Core.State.Entity;

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

public enum Operator
{
    Equal,
    Add,
    Sub,
}

public static class OperatorSever
{
    public static Operator StringToOperator(string stringOp)
    {
        return stringOp switch
        {
            "=" => Operator.Equal,
            "+=" or "+" => Operator.Add,
            "-=" or "-" => Operator.Sub,
            _ => throw new ArgumentException($"Quantity 不支持 '{stringOp}'", stringOp)
        };
    }

    public static object ChangedValue(this Operator op, object? oldValue, object newValue)
    {
        if (oldValue == null)
            return newValue;

        switch (oldValue)
        {
            case int value: return op.ChangeNumberValue(value, newValue);
            case float value: return op.ChangeNumberValue(value, newValue);
            case double value: return op.ChangeNumberValue(value, newValue);
        }

        switch (oldValue)
        {
            case TypedID value: return op.ChangeTypedIdValue(value, newValue);
            case string value: return op.ChangeStringValue(value, newValue);
            case List<object> value: return op.ChangeListValue(value, newValue);
            case Dictionary<string, object> value: return op.ChangeDictionaryValue(value, newValue);
        }

        return oldValue;
    }

    private static TypedID ChangeTypedIdValue(this Operator op, TypedID oldValue, object newValue)
    {
        if (newValue is not TypedID newTypedId)
        {
            Log.Warning($"newValue 必须是 TypedID 类型，但传入的值是 {newValue.GetType()}");
            return oldValue;
        }

        switch (op)
        {
            case Operator.Equal:
                return newTypedId;
            case Operator.Add:
            case Operator.Sub:
                Log.Warning($"TypedID 不支持 '{op}' 操作");
                return oldValue;
            default:
                throw new UnreachableException($"不可能到达的分支 '{op}'");
        }
    }

    private static Dictionary<string, TValue> ChangeDictionaryValue<TValue>(this Operator op,
        Dictionary<string, TValue> oldDict, object newValue)
    {
        var result = new Dictionary<string, TValue>(oldDict);

        switch (op)
        {
            case Operator.Add:
                if (newValue is Dictionary<string, TValue> newDict)
                {
                    foreach (var kvp in newDict.Where(kvp => !result.ContainsKey(kvp.Key)))
                        result.Add(kvp.Key, kvp.Value);
                }
                else if (newValue is KeyValuePair<string, TValue> kvp)
                {
                    if (!result.ContainsKey(kvp.Key))
                        result.Add(kvp.Key, kvp.Value);
                }
                else
                    Log.Error($"newValue 必须是 Dictionary 或 KeyValuePair，但传入的值是 {newValue.GetType()}");

                break;
            case Operator.Sub:
                if (newValue is List<string> keysToRemove)
                {
                    foreach (string key in keysToRemove.Where(key => result.ContainsKey(key)))
                        result.Remove(key);
                }
                else if (newValue is string keyToRemove)
                {
                    result.Remove(keyToRemove);
                }
                else
                    Log.Error($"newValue 此时必须是 List<string> 或 string，但传入的值是 {newValue.GetType()}");

                break;

            case Operator.Equal:
                if (newValue is Dictionary<string, TValue> dict)
                    return new Dictionary<string, TValue>(dict);
                Log.Warning($"newValue 必须是 Dictionary<TKey, TValue>，但传入的值是 {newValue.GetType()}");
                break;

            default:
                throw new UnreachableException($"不可能到达的分支 '{op}'");
        }

        return result;
    }

    private static List<object> ChangeListValue(this Operator op, List<object> oldValue, object newValue)
    {
        var result = new List<object>(oldValue);
        if (newValue is IList newIList)
        {
            switch (op)
            {
                case Operator.Add:
                    result = result.Concat(newIList.Cast<object>()).ToList();
                    break;
                case Operator.Sub:
                    result = result.Except(newIList.Cast<object>()).ToList();
                    break;
                case Operator.Equal:
                    return newIList.Cast<object>().ToList();
                default:
                    throw new UnreachableException($"不可能到达的分支 '{op}'");
            }
        }
        else
        {
            switch (op)
            {
                case Operator.Add:
                    result.Add(newValue);
                    break;
                case Operator.Sub:
                    result.Remove(newValue);
                    break;
                case Operator.Equal:
                    return [newValue];
                default:
                    throw new UnreachableException($"不可能到达的分支 '{op}'");
            }
        }

        return result;
    }

    private static string ChangeStringValue(this Operator op, string oldValue, object newValue)
    {
        string newString;
        if (newValue is string value)
        {
            newString = value;
        }
        else
        {
            Log.Info($" 'ChangeStringValue' 操作需要string，但传入的值 '{newValue}' 不是string，已自动处理");
            newString = newValue.ToString() ?? string.Empty;
        }

        switch (op)
        {
            case Operator.Add: return $"{oldValue}{newString}";
            case Operator.Sub:
                Log.Warning($" 'ChangeStringValue' 操作 '{op}' 不支持，已自动处理。");
                return oldValue;
            case Operator.Equal:
                return newString;
            default:
                throw new UnreachableException($"不可能到达的分支 '{op}'");
        }
    }


    private static T ChangeNumberValue<T>(this Operator op, T oldValue, object newValue) where T : INumber<T>
    {
        if (newValue is not T)
        {
            Log.Warning($" 'ChangeNumberValue' 操作需要数值，但传入的值 '{newValue}' 不是数。");
            return oldValue;
        }

        var changeTypeValue = (T)Convert.ChangeType(newValue, typeof(T));
        switch (op)
        {
            case Operator.Add: return oldValue + changeTypeValue;
            case Operator.Sub: return oldValue - changeTypeValue;
            case Operator.Equal:
                return changeTypeValue;
            default:
                throw new UnreachableException($"不可能到达的分支 '{op}'");
        }
    }
}

/// <summary>
/// 类型化的实体ID，用于精确引用。使用 record struct 实现值相等性。
/// </summary>
public readonly record struct TypedID(EntityType Type, string Id)
{
    // 提供一个方便的字符串表示，用于日志或调试
    public override string ToString() =>
        $"{this.Type}:{this.Id}";
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
        if (key.Equals("quantity", StringComparison.OrdinalIgnoreCase))
        {
            base.SetAttribute(key, value is int quantityValue and >= 0 ? quantityValue : 0);
        }
        else
        {
            base.SetAttribute(key, value);
        }
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