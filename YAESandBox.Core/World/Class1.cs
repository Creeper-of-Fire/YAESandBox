#nullable enable // 启用 nullable 上下文

using System;
using System.Collections.Generic;
// For TryGetValue
using System.Linq;
using System.Numerics;
using ArgumentException = System.ArgumentException; // For LINQ operations if needed later, like in FindEntityByName

// 定义核心命名空间
namespace YAESandBox.Core.World;

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

public interface ILogger
{
    void Log(string message);
    void Warning(string message);
    void Error(string message);
    void Debug(string message);
}

public class OperatorSever(ILogger logger)
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

    public static object ChangeValue(this Operator op, object oldValue, object newValue)
    {
        if (op == Operator.Equal)
            return newValue;

        switch (oldValue)
        {
            case int value: return op.ChangeNumberValue(value, (int)newValue);
            case float value: return op.ChangeNumberValue(value, (float)newValue);
            case double value: return op.ChangeNumberValue(value, (double)newValue);
        }

        switch (oldValue)
        {
        }
    }

    public static bool isINumber(object value)
    {
        return value switch
        {
            int => true,
            float => true,
            double => true,
            _ => false
        };
    }

    private static T ChangeNumberValue<T>(this Operator op, T oldValue, object newValue) where T : INumber<T>
    {
        if (isINumber(newValue))
            return
        switch (op)
        {
            Operator.Equal:return oldValue + newValue;
        }

        return oldValue + newValue;
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

    public override void ModifyAttribute(string key, Operator op, object? value)
    {
        // logger.LogDebug($"Item '{this.TypedId}': (Core) ModifyAttribute: Key='{key}', Op='{op}', Value='{value?.ToString() ?? "null"}'");

        if (key.Equals("quantity", StringComparison.OrdinalIgnoreCase))
        {
            // 获取当前值，注意处理 null 的情况
            object? currentQuantityObj = this.GetAttribute(key, 0); // 默认值设为 0
            if (currentQuantityObj is not int currentQuantity)
            {
                // 如果获取到的不是 int (虽然 SetAttribute 应该保证了)，则抛出错误或赋默认值
                currentQuantity = 0;
                // logger.LogWarning($"Item '{this.TypedId}': Quantity 属性不是有效的整数，重置为 0。");
            }

            int? newQuantity = op switch
            {
                "=" when value is int intValue => intValue,
                "=" => throw new ArgumentException("Quantity '=' 操作需要整数值", nameof(value)),
                "+=" or "+" when value is int intValue => currentQuantity + intValue,
                "+=" or "+" => throw new ArgumentException($"Quantity '{op}' 操作需要整数值", nameof(value)),
                "-=" or "-" when value is int intValue => currentQuantity - intValue,
                "-=" or "-" => throw new ArgumentException($"Quantity '{op}' 操作需要整数值", nameof(value)),
            };

            // 调用 SetAttribute 进行验证和设置
            this.SetAttribute(key, newQuantity);
        }
        else
        {
            // logger.LogDebug($"Item '{this.TypedId}': 委托给基类 for key '{key}'");
            base.ModifyAttribute(key, op, value);
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