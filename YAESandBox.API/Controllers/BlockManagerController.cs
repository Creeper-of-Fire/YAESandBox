// --- START OF FILE BlockManagementController.cs ---

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Nito.AsyncEx;
using YAESandBox.API.DTOs; // 可能需要新的 DTO
using YAESandBox.API.Services; // 需要 IBlockWritService (或新的 IBlockManagementService)
using YAESandBox.Core.Block; // For BlockManager/Block
using YAESandBox.Core.State; // For WorldState, GameState
using YAESandBox.Depend; // For Log

namespace YAESandBox.API.Controllers;

/// <summary>
/// 提供手动管理 Block（创建、删除、移动）的 API 端点。
/// 这些操作通常用于调试、测试或未来的管理界面。
/// </summary>
[ApiController]
[Route("api/manage/blocks")]
public class BlockManagementController(
    IBlockManagementService blockManagementService,
    IBlockReadService blockReadService)
    : ControllerBase
{
    private readonly IBlockManagementService _blockManagementService = blockManagementService;
    private readonly IBlockReadService _blockReadService = blockReadService;


    /// <summary>
    /// 手动创建一个新的、空的 Block。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BlockDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)] // If parent not found
    [ProducesResponseType(StatusCodes.Status409Conflict)] // If parent state invalid
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HiddenFromJsonApi]
    public async Task<IActionResult> CreateBlockManually([FromBody] CreateBlockManualRequestDto request)
    {
        if (!ModelState.IsValid) // 使用模型验证
        {
            return BadRequest(ModelState);
        }

        var (result, newBlockStatus) =
            await _blockManagementService.CreateBlockManuallyAsync(request.ParentBlockId, request.InitialMetadata);

        switch (result)
        {
            case ManagementResult.Success:
                if (newBlockStatus == null) // 理论上不应发生
                {
                    Log.Error("CreateBlockManuallyAsync 返回 Success 但 BlockStatus 为 null。");
                    return StatusCode(StatusCodes.Status500InternalServerError, "创建成功但无法获取 Block 信息。");
                }

                var newBlockDto = MapToDetailDto(newBlockStatus); // 使用辅助方法映射
                // 使用 GetBlockDetail 的路由名和参数创建 Location Header
                return CreatedAtAction(nameof(BlocksController.GetBlockDetail), "Blocks",
                    new { blockId = newBlockStatus.Block.BlockId }, newBlockDto);
            case ManagementResult.NotFound:
                return NotFound($"父 Block '{request.ParentBlockId}' 未找到。");
            case ManagementResult.InvalidState:
                return Conflict($"父 Block '{request.ParentBlockId}' 当前状态不允许创建子节点。"); // 409 Conflict 更合适
            case ManagementResult.BadRequest:
                return BadRequest("请求无效。");
            case ManagementResult.Error:
            default:
                return StatusCode(StatusCodes.Status500InternalServerError, "创建 Block 时发生内部错误。");
        }
    }

    /// <summary>
    /// 手动删除一个指定的 Block。
    /// </summary>
    [HttpDelete("{blockId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)] // For InvalidState
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteBlockManually(string blockId, [FromQuery] bool recursive = false,
        [FromQuery] bool force = false)
    {
        var result = await _blockManagementService.DeleteBlockManuallyAsync(blockId, recursive, force);

        return result switch
        {
            ManagementResult.Success => NoContent(),
            ManagementResult.NotFound => NotFound($"Block '{blockId}' 未找到。"),
            ManagementResult.CannotPerformOnRoot => BadRequest("不允许删除根节点。"),
            ManagementResult.InvalidState => Conflict($"Block '{blockId}' 当前状态不允许删除（可尝试 force=true）。"),
            ManagementResult.BadRequest => BadRequest("请求无效（例如 Block 有子节点但未指定 recursive=true）。"),
            ManagementResult.Error => StatusCode(StatusCodes.Status500InternalServerError, "删除 Block 时发生错误。"),
            _ => StatusCode(StatusCodes.Status500InternalServerError, "未知的删除结果。")
        };
    }

    /// <summary>
    /// 手动将一个 Block（及其子树）移动到另一个父 Block 下。
    /// </summary>
    [HttpPost("{blockId}/move")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)] // For InvalidState or CyclicMove
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [HiddenFromJsonApi]
    public async Task<IActionResult> MoveBlockManually(string blockId, [FromBody] MoveBlockRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (blockId == request.NewParentBlockId) // 基本的循环检查
        {
            return Conflict("不能将 Block 移动到自身之下。");
        }

        var result = await _blockManagementService.MoveBlockManuallyAsync(blockId, request.NewParentBlockId);

        return result switch
        {
            ManagementResult.Success => NoContent(),
            ManagementResult.NotFound => NotFound(
                $"要移动的 Block '{blockId}' 或目标父 Block '{request.NewParentBlockId}' 未找到。"),
            ManagementResult.CannotPerformOnRoot => BadRequest("不允许移动根节点。"),
            ManagementResult.InvalidState => Conflict("Block 或目标父 Block 当前状态不允许移动。"),
            ManagementResult.CyclicOperation => Conflict("不能将 Block 移动到其自身或其子孙节点下。"),
            ManagementResult.BadRequest => BadRequest("请求无效。"),
            ManagementResult.Error => StatusCode(StatusCodes.Status500InternalServerError, "移动 Block 时发生错误。"),
            _ => StatusCode(StatusCodes.Status500InternalServerError, "未知的移动结果。")
        };
    }

    // --- 辅助方法 MapToDetailDto (应移至公共位置或由 ReadService 提供) ---
    private BlockDetailDto MapToDetailDto(BlockStatus blockStatus)
    {
        var block = blockStatus.Block;
        return new BlockDetailDto
        {
            BlockId = block.BlockId,
            ParentBlockId = block.ParentBlockId,
            StatusCode = blockStatus.StatusCode,
            BlockContent = block.BlockContent,
            Metadata = new Dictionary<string, string>(block.Metadata),
            ChildrenInfo = new List<string>(block.ChildrenList)
        };
    }
}

// --- DTOs for BlockManagementController ---

/// <summary>
/// 手动创建 Block 的请求体。
/// </summary>
public record CreateBlockManualRequestDto
{
    /// <summary>
    /// 新 Block 的父 Block ID。必须指定一个有效的、存在的 Block ID。
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public string ParentBlockId { get; init; } = null!;

    /// <summary>
    /// (可选) 新 Block 的初始元数据。
    /// </summary>
    public Dictionary<string, string>? InitialMetadata { get; init; }
}

/// <summary>
/// 手动移动 Block 的请求体。
/// </summary>
public record MoveBlockRequestDto
{
    /// <summary>
    /// Block 要移动到的新父节点的 ID。必须指定一个有效的、存在的 Block ID。
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public string NewParentBlockId { get; init; } = null!;
}

// --- Enums for Operation Results (需要在 BlockManager 或共享位置定义) ---

/// <summary>
/// 手动删除 Block 的操作结果。
/// </summary>
public enum DeleteResult
{
    Success,
    NotFound,
    CannotDeleteRoot,
    InvalidState,
    Error
}

/// <summary>
/// 手动移动 Block 的操作结果。
/// </summary>
public enum MoveResult
{
    Success,
    NotFound,
    CannotMoveRoot,
    InvalidState,
    CyclicMove,
    Error
}

// --- END OF FILE BlockManagementController.cs ---