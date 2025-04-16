using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity; // For EntityType, TypedID

namespace YAESandBox.API.Controllers;

[ApiController]
[Route("api/entities")] // /api/entities
public class EntitiesController(BlockService blockService) : ControllerBase
{
    private BlockService blockService { get; } = blockService;

    /// <summary>
    /// 获取指定 Block 当前可交互 WorldState 中的所有实体摘要。
    /// 需要 block_id 作为查询参数。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EntitySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // Missing block_id
    [ProducesResponseType(StatusCodes.Status404NotFound)]   // Block not found
    public async Task<IActionResult> GetAllEntities([FromQuery, Required] string blockId)
    {
        // BlockManager needs a method to get entities based on blockId and its current target WorldState
        var entities = await this.blockService.GetAllEntitiesSummaryAsync(blockId);

        if (entities == null) // Indicates block not found or other issue handled in service
        {
            return this.NotFound($"Block with ID '{blockId}' not found or inaccessible.");
        }

        // Map Core Entities to DTOs (implement this mapping)
        var dtos = this.MapToSummaryDtos(entities);
        return this.Ok(dtos);
    }

    /// <summary>
    /// 获取指定 Block 当前可交互 WorldState 中的单个实体详细信息。
    /// 需要 block_id 作为查询参数。
    /// </summary>
    [HttpGet("{entityType}/{entityId}")]
    [ProducesResponseType(typeof(EntityDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // Missing block_id
    [ProducesResponseType(StatusCodes.Status404NotFound)]   // Block or Entity not found
    public async Task<IActionResult> GetEntityDetail(EntityType entityType, string entityId, [FromQuery, Required] string blockId)
    {
        var typedId = new TypedID(entityType, entityId);
        var entity = await this.blockService.GetEntityDetailAsync(blockId, typedId);

        if (entity == null)
        {
            return this.NotFound($"Entity '{typedId}' not found in block '{blockId}' or block not found.");
        }

        // Map Core Entity to DTO (implement this mapping)
        var dto = this.MapToDetailDto(entity);
        return this.Ok(dto);
    }


    // --- Helper Mapping Functions (Implement these based on your Core/DTO structures) ---

    private IEnumerable<EntitySummaryDto> MapToSummaryDtos(IEnumerable<BaseEntity> entities)
    {
        // Example basic mapping
        return entities.Select(e => new EntitySummaryDto
        {
            EntityId = e.EntityId,
            EntityType = e.EntityType,
            IsDestroyed = e.IsDestroyed,
            Name = e.GetAttribute("name") as string // Assuming 'name' attribute exists
            // Map other summary fields if needed
        });
    }

    private EntityDetailDto MapToDetailDto(BaseEntity entity)
    {
         // Example basic mapping
        return new EntityDetailDto
        {
            EntityId = entity.EntityId,
            EntityType = entity.EntityType,
            IsDestroyed = entity.IsDestroyed,
            Name = entity.GetAttribute("name") as string, // Assuming 'name' attribute exists
            Attributes = entity.GetAllAttributes() // Get all attributes for detail view
            // Map other summary fields if needed
        };
    }

     // Potentially add more specific entity query endpoints here if needed
    // e.g., GET /places/{placeId}/contents?block_id=...
    // e.g., GET /characters/{characterId}/items?block_id=...
}