﻿using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;

// For EntityType, Operator

namespace YAESandBox.Core.Action;

/// <summary>
/// 代表原子操作的类型。
/// </summary>
public enum AtomicOperationType
{
    CreateEntity,
    ModifyEntity,
    DeleteEntity
}

/// <summary>
/// 封装一个原子化操作及其参数。
/// 使用 record struct 以获得值相等性和不变性。
/// </summary>
public record AtomicOperation
{
    public required AtomicOperationType OperationType { get; init; }
    public required EntityType EntityType { get; init; }
    public required string EntityId { get; init; }

    // --- CreateEntity 参数 ---
    /// <summary>
    /// (仅 CreateEntity) 要创建的实体的初始属性。
    /// </summary>
    public Dictionary<string, object?>? InitialAttributes { get; init; }

    // --- ModifyEntity 参数 ---
    /// <summary>
    /// (仅 ModifyEntity) 要修改的属性键。
    /// </summary>
    public string? AttributeKey { get; init; }

    /// <summary>
    /// (仅 ModifyEntity) 修改操作符 (=, +, -)。
    /// </summary>
    public Operator? ModifyOperator { get; init; }

    /// <summary>
    /// (仅 ModifyEntity) 修改操作的值。
    /// </summary>
    public object? ModifyValue { get; init; }

    // --- DeleteEntity 参数 (暂时不需要额外参数) ---

    // 为了方便，可以添加静态工厂方法来创建不同类型的操作
    public static AtomicOperation Create(EntityType type, string id, Dictionary<string, object?>? attributes = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Entity ID cannot be null or whitespace.", nameof(id));

        return new AtomicOperation
        {
            OperationType = AtomicOperationType.CreateEntity,
            EntityType = type,
            EntityId = id,
            InitialAttributes = attributes ?? new Dictionary<string, object?>() // 确保不为 null
        };
    }

    public static AtomicOperation Modify(EntityType type, string id, string attributeKey, Operator op, object? value)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Entity ID cannot be null or whitespace.", nameof(id));
        if (string.IsNullOrWhiteSpace(attributeKey))
            throw new ArgumentException("Attribute key cannot be null or whitespace.", nameof(attributeKey));

        return new AtomicOperation
        {
            OperationType = AtomicOperationType.ModifyEntity,
            EntityType = type,
            EntityId = id,
            AttributeKey = attributeKey,
            ModifyOperator = op,
            ModifyValue = value
        };
    }

    public static AtomicOperation Modify(EntityType type, string id, string attributeKey, string stringOp,
        object? value)
    {
        return Modify(type, id, attributeKey, OperatorHelper.StringToOperator(stringOp), value);
    }

    public static AtomicOperation Delete(EntityType type, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Entity ID cannot be null or whitespace.", nameof(id));

        return new AtomicOperation
        {
            OperationType = AtomicOperationType.DeleteEntity,
            EntityType = type,
            EntityId = id
        };
    }
}

/// <summary>
/// 操作错误，通常来说可以忽略
/// </summary>
/// <param name="Code"></param>
/// <param name="Message"></param>
/// <param name="FailedOperation"></param>
public record OperationHandledIssue(BlockResultCode Code, string Message, AtomicOperation FailedOperation) : Error(Message)
{
    public static OperationHandledIssue NotFound(AtomicOperation operation, string message)
    {
        return new OperationHandledIssue(BlockResultCode.NotFound, message, operation);
    }

    public static OperationHandledIssue Conflict(AtomicOperation operation, string message)
    {
        return new OperationHandledIssue(BlockResultCode.Conflict, message, operation);
    }

    public static OperationHandledIssue InvalidInput(AtomicOperation operation, string message)
    {
        return new OperationHandledIssue(BlockResultCode.InvalidInput, message, operation);
    }

    public static OperationHandledIssue Error(AtomicOperation operation, string message)
    {
        return new OperationHandledIssue(BlockResultCode.Error, message, operation);
    }
}