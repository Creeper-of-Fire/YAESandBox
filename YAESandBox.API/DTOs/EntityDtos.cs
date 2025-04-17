using YAESandBox.Core.State.Entity; // For EntityType, TypedID

namespace YAESandBox.API.DTOs;

/// <summary>
/// 用于 API 响应的实体基本信息。
/// </summary>
public record EntitySummaryDto
{
    public string EntityId { get; set; } = null!;
    public EntityType EntityType { get; set; }
    public bool IsDestroyed { get; set; }
    // 可能包含 Name 或 Description 等关键摘要信息
    public string? Name { get; set; }
}

/// <summary>
/// 用于 API 响应的实体详细信息（包含所有属性）。
/// </summary>
public record EntityDetailDto : EntitySummaryDto
{
    public Dictionary<string, object?> Attributes { get; init; } = new();
}

// 可以为特定实体类型创建更具体的 DTO，如果需要的话
// public class ItemDetailDto : EntityDetailDto { public int Quantity { get; set; } }
// public class PlaceDetailDto : EntityDetailDto { public List<TypedID> Contents { get; set; } }