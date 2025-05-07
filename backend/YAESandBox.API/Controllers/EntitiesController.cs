using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services.InterFaceAndBasic;
using YAESandBox.Core.State.Entity; // For EntityType, TypedID

namespace YAESandBox.API.Controllers;

/// <summary>
/// 提供与实体 (Entity) 相关的 API 端点，用于查询指定 Block 中的实体信息。
/// </summary>
[ApiController]
[Route("api/entities")] // /api/entities
public class EntitiesController(IBlockWritService writServices, IBlockReadService readServices)
    : APIControllerBase(readServices, writServices)
{
    /// <summary>
    /// 获取指定 Block 当前可交互 WorldState 中的所有非销毁实体摘要信息。
    /// </summary>
    /// <param name="blockId" required="true">要查询的目标 Block 的 ID。</param>
    /// <returns>包含实体摘要信息 DTO 的可枚举集合。</returns>
    /// <response code="200">成功返回实体摘要列表。</response>
    /// <response code="400">缺少必需的 'blockId' 查询参数。</response>
    /// <response code="404">未找到具有指定 ID 的 Block 或 Block 无法访问。</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EntitySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // Missing blockId
    [ProducesResponseType(StatusCodes.Status404NotFound)] // Block not found
    public async Task<IActionResult> GetAllEntities([FromQuery] [Required] string blockId)
    {
        // BlockManager 需要一个方法来根据 blockId 及其当前目标 WorldState 获取实体
        var entities = await this.blockReadService.GetAllEntitiesSummaryAsync(blockId);

        if (entities == null) // 表示 block 未找到或服务层处理了其他问题
            return this.NotFound($"未找到 ID 为 '{blockId}' 的 Block 或无法访问。");

        // 将核心实体映射为 DTO (实现此映射)
        var dtos = this.MapToSummaryDtos(entities);
        return this.Ok(dtos);
    }

    /// <summary>
    /// 获取指定 Block 当前可交互 WorldState 中的单个非销毁实体的详细信息。
    /// </summary>
    /// <param name="entityType">要查询的实体的类型 (Item, Character, Place)。</param>
    /// <param name="entityId">要查询的实体的 ID。</param>
    /// <param name="blockId" required="true">目标 Block 的 ID。</param>
    /// <returns>包含实体详细信息的 DTO。</returns>
    /// <response code="200">成功返回实体详细信息。</response>
    /// <response code="400">缺少必需的 'blockId' 查询参数。</response>
    /// <response code="404">未在指定 Block 中找到实体，或 Block 未找到。</response>
    [HttpGet("{entityType}/{entityId}")]
    [ProducesResponseType(typeof(EntityDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // Missing blockId or invalid entityType/entityId format
    [ProducesResponseType(StatusCodes.Status404NotFound)] // Block or Entity not found
    public async Task<IActionResult> GetEntityDetail(EntityType entityType, string entityId, [FromQuery] [Required] string blockId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
            return this.BadRequest("实体 ID 不能为空。");

        var typedId = new TypedID(entityType, entityId);
        var entity = await this.blockReadService.GetEntityDetailAsync(blockId, typedId);

        if (entity == null)
            return this.NotFound($"在 Block '{blockId}' 中未找到实体 '{typedId}'，或 Block 未找到。");

        // 将核心实体映射为 DTO (实现此映射)
        var dto = this.MapToDetailDto(entity);
        return this.Ok(dto);
    }


    // --- 辅助映射函数 (根据你的 Core/DTO 结构实现这些) ---

    /// <summary>
    /// 将核心实体集合映射为实体摘要 DTO 集合。
    /// </summary>
    private IEnumerable<EntitySummaryDto> MapToSummaryDtos(IEnumerable<BaseEntity> entities)
    {
        // 示例基础映射
        return entities.Select(e => new EntitySummaryDto
        {
            EntityId = e.EntityId,
            EntityType = e.EntityType,
            IsDestroyed = e.IsDestroyed,
            Name = e.GetAttribute("name") as string ?? e.EntityId // 假设 'name' 属性存在，否则用ID
            // 如果需要，映射其他摘要字段
        });
    }

    /// <summary>
    /// 将单个核心实体映射为实体详细信息 DTO。
    /// </summary>
    private EntityDetailDto MapToDetailDto(BaseEntity entity)
    {
        // 示例基础映射
        return new EntityDetailDto
        {
            EntityId = entity.EntityId,
            EntityType = entity.EntityType,
            IsDestroyed = entity.IsDestroyed,
            Name = entity.GetAttribute("name") as string ?? entity.EntityId, // 假设 'name' 属性存在
            Attributes = entity.GetAllAttributes() // 获取详细视图的所有属性
            // 如果需要，映射其他摘要字段
        };
    }

    // 如果需要，可以在此处添加更具体的实体查询端点
    // 例如：GET /places/{placeId}/contents?block_id=...
    // 例如：GET /characters/{characterId}/items?block_id=...
}