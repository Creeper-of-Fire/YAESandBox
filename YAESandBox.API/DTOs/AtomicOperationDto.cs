using System.ComponentModel.DataAnnotations;
using YAESandBox.Core.Action; // For Operator enum reference if needed, or use strings
using YAESandBox.Core.State.Entity; // For EntityType

namespace YAESandBox.API.DTOs;

/// <summary>
/// 用于 API 请求的原子操作表示。
/// </summary>
public record AtomicOperationRequestDto
{
    [Required] public string OperationType { get; set; } = null!; // "create", "modify", "delete"

    [Required] public EntityType EntityType { get; set; }

    [Required] [MinLength(1)] public string EntityId { get; set; } = null!;

    // --- Create ---
    public Dictionary<string, object?>? InitialAttributes { get; set; }

    // --- Modify ---
    public string? AttributeKey { get; set; }
    public string? ModifyOperator { get; set; } // Use string like "=", "+=", "-=" for flexibility
    public object? ModifyValue { get; set; }

    // No extra fields needed for Delete
}

/// <summary>
/// 库
/// </summary>
public static class AtomicOperationRequestDtoHelper
{
    public static AtomicOperationRequestDto ToAtomicOperationRequestDto(this AtomicOperation operation)
    {
        return new AtomicOperationRequestDto
        {
            OperationType = operation.OperationType.ToString(),
            EntityType = operation.EntityType,
            EntityId = operation.EntityId,
            AttributeKey = operation.AttributeKey,
            ModifyOperator = OperatorHelper.OperatorToString(operation.ModifyOperator),
            ModifyValue = operation.ModifyValue,
            InitialAttributes = operation.InitialAttributes
        };
    }

    public static AtomicOperation ToAtomicOperation(this AtomicOperationRequestDto dto)
    {
        return new AtomicOperation
        {
            OperationType = dto.OperationType switch
            {
                "CreateEntity" => AtomicOperationType.CreateEntity,
                "ModifyEntity" => AtomicOperationType.ModifyEntity,
                "DeleteEntity" => AtomicOperationType.DeleteEntity,
                _ => throw new ArgumentException("Invalid operation type")
            },
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            AttributeKey = dto.AttributeKey,
            ModifyOperator = OperatorHelper.StringToOperatorCanBeNull(dto.ModifyOperator),
            ModifyValue = dto.ModifyValue,
            InitialAttributes = dto.InitialAttributes
        };
    }

    public static List<AtomicOperation> ToAtomicOperations(this List<AtomicOperationRequestDto> dto)
    {
        return dto.Select(ToAtomicOperation).ToList();
    }

    public static List<AtomicOperationRequestDto> ToAtomicOperationRequests(this List<AtomicOperation> operations)
    {
        return operations.Select(ToAtomicOperationRequestDto).ToList();
    }
}

/// <summary>
/// 用于 API 请求的批量原子操作。
/// </summary>
public record BatchAtomicRequestDto
{
    [Required] [MinLength(1)] public List<AtomicOperationRequestDto> Operations { get; set; } = new();
}