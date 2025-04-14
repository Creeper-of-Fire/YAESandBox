using System.ComponentModel.DataAnnotations;
using YAESandBox.Core.Action; // For Operator enum reference if needed, or use strings
using YAESandBox.Core.State.Entity;  // For EntityType

namespace YAESandBox.API.DTOs;

/// <summary>
/// 用于 API 请求的原子操作表示。
/// </summary>
public class AtomicOperationRequestDto
{
    [Required]
    public string OperationType { get; set; } = null!; // "create", "modify", "delete"

    [Required]
    public EntityType EntityType { get; set; }

    [Required]
    [MinLength(1)]
    public string EntityId { get; set; } = null!;

    // --- Create ---
    public Dictionary<string, object?>? InitialAttributes { get; set; }

    // --- Modify ---
    public string? AttributeKey { get; set; }
    public string? ModifyOperator { get; set; } // Use string like "=", "+=", "-=" for flexibility
    public object? ModifyValue { get; set; }

    // No extra fields needed for Delete
}

/// <summary>
/// 用于 API 请求的批量原子操作。
/// </summary>
public class BatchAtomicRequestDto
{
    [Required]
    [MinLength(1)]
    public List<AtomicOperationRequestDto> Operations { get; set; } = new();
}