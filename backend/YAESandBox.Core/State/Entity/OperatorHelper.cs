using System.Collections;
using System.Diagnostics;
using System.Numerics;
using YAESandBox.Depend;

namespace YAESandBox.Core.State.Entity;

public enum Operator
{
    Equal,
    Add,
    Sub
}

public static class OperatorHelper
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="stringOp"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">不支持的操作符</exception>
    public static Operator StringToOperator(string stringOp)
    {
        return stringOp switch
        {
            "=" => Operator.Equal,
            "+=" or "+" => Operator.Add,
            "-=" or "-" => Operator.Sub,
            _ => throw new ArgumentException($"不支持 '{stringOp}'")
        };
    }

    public static Operator? StringToOperatorCanBeNull(string? stringOp)
    {
        if (string.IsNullOrWhiteSpace(stringOp))
            return null;
        return StringToOperator(stringOp);
    }

    public static string? OperatorToString(Operator? op)
    {
        if (op == null)
            return null;
        return op switch
        {
            Operator.Equal => "=",
            Operator.Add => "+=",
            Operator.Sub => "-=",
            _ => throw new ArgumentException($"Quantity 不支持 '{op}'", op.ToString())
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
                switch (newValue)
                {
                    case Dictionary<string, TValue> newDict:
                    {
                        foreach (var kvp in newDict.Where(kvp => !result.ContainsKey(kvp.Key)))
                            result.Add(kvp.Key, kvp.Value);
                        break;
                    }
                    case KeyValuePair<string, TValue> kvp:
                    {
                        if (!result.ContainsKey(kvp.Key))
                            result.Add(kvp.Key, kvp.Value);
                        break;
                    }
                    default:
                        Log.Error($"newValue 必须是 Dictionary 或 KeyValuePair，但传入的值是 {newValue.GetType()}");
                        break;
                }

                break;
            case Operator.Sub:
                switch (newValue)
                {
                    case List<string> keysToRemove:
                    {
                        foreach (string key in keysToRemove.Where(key => result.ContainsKey(key)))
                            result.Remove(key);
                        break;
                    }
                    case string keyToRemove:
                        result.Remove(keyToRemove);
                        break;
                    default:
                        Log.Error($"newValue 此时必须是 List<string> 或 string，但传入的值是 {newValue.GetType()}");
                        break;
                }

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
        else
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