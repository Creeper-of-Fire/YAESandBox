using System.ComponentModel.DataAnnotations;
using YAESandBox.Seed.State.Entity;

// For EntityType

namespace YAESandBox.Seed.DTOs;

/// <summary>
/// 用于 API 响应，表示实体的基本摘要信息。
/// </summary>
public record EntitySummaryDto
{
    /// <summary>
    /// 实体的唯一 ID。
    /// </summary>
    [Required]
    public required string EntityId { get; set; }

    /// <summary>
    /// 实体的类型 (Item, Character, Place)。
    /// </summary>
    [Required]
    public required EntityType EntityType { get; set; }

    /// <summary>
    /// 指示实体是否已被标记为销毁。
    /// 注意：查询 API 通常只返回未销毁的实体。
    /// </summary>
    [Required]
    public required bool IsDestroyed { get; set; }

    /// <summary>
    /// 实体的名称 (通常来自 'name' 属性，如果不存在则可能回退到 EntityId)。
    /// </summary>
    public string? Name { get; set; }
    // 可以添加其他关键摘要信息，如描述等
}

/// <summary>
/// 用于 API 响应，表示实体的详细信息，包含所有属性。
/// 继承自 <see cref="EntitySummaryDto"/>。
/// </summary>
public record EntityDetailDto : EntitySummaryDto
{
    /// <summary>
    /// 包含实体所有属性（包括核心属性如 IsDestroyed 和动态属性）的字典。
    /// 值的类型可能是 string, int, bool, double, List[object?], Dictionary-[string, object?], TypedID 等。
    /// </summary>
    [Required]
    public required Dictionary<string, object?> Attributes { get; init; } = new();
}

// 如果需要，可以为特定实体类型创建更具体的 DTO
// public record ItemDetailDto : EntityDetailDto { public int Quantity { get; set; } }
// public record PlaceDetailDto : EntityDetailDto { public List<TypedID> Contents { get; set; } }