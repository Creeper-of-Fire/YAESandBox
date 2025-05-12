// --- START OF FILE BlockManagementController.cs ---

using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services.InterFaceAndBasic;
using YAESandBox.Depend;
using static YAESandBox.API.GlobalSwaggerConstants;

namespace YAESandBox.API.Controllers;

/// <summary>
/// 提供手动管理 Block（创建、删除、移动）的 API 端点。
/// 这些操作通常用于调试、测试或未来的管理界面。
/// </summary>
[ApiController]
[Route("api/manage/blocks")]
public class BlockManagementController(
    IBlockManagementService blockManagementService,
    IBlockWritService writServices,
    IBlockReadService readServices)
    : APIControllerBase(readServices, writServices)
{
    private IBlockManagementService blockManagementService { get; } = blockManagementService;


    /// <summary>
    /// 手动创建一个新的、空的 Block。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BlockDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)] // If parent not found
    [ProducesResponseType(StatusCodes.Status409Conflict)] // If parent state invalid
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ApiExplorerSettings(GroupName = InternalApiGroupName)]
    [Tags("__DEBUG__")]
    public async Task<IActionResult> CreateBlockManually([FromBody] CreateBlockManualRequestDto request)
    {
        if (!this.ModelState.IsValid) // 使用模型验证
            return this.BadRequest(this.ModelState);

        var (result, newBlockStatus) =
            await this.blockManagementService.CreateBlockManuallyAsync(request.ParentBlockId, request.InitialMetadata);

        switch (result)
        {
            case ManagementResult.Success:
                if (newBlockStatus == null) // 理论上不应发生
                {
                    Log.Error("CreateBlockManuallyAsync 返回 Success 但 BlockStatus 为 null。");
                    return this.StatusCode(StatusCodes.Status500InternalServerError, "创建成功但无法获取 Block 信息。");
                }

                var newBlockDto = newBlockStatus.MapToDetailDto(); // 使用辅助方法映射
                // 使用 GetBlockDetail 的路由名和参数创建 Location Header
                return this.CreatedAtAction(nameof(BlocksController.GetBlockDetail), "Blocks",
                    new { blockId = newBlockStatus.Block.BlockId }, newBlockDto);
            case ManagementResult.NotFound:
                return this.NotFound($"父 Block '{request.ParentBlockId}' 未找到。");
            case ManagementResult.InvalidState:
                return this.Conflict($"父 Block '{request.ParentBlockId}' 当前状态不允许创建子节点。"); // 409 Conflict 更合适
            case ManagementResult.BadRequest:
                return this.BadRequest("请求无效。");
            case ManagementResult.Error:
            default:
                return this.StatusCode(StatusCodes.Status500InternalServerError, "创建 Block 时发生内部错误。");
        }
    }

    //TODO 增加一个复制节点的功能

    /// <summary>
    /// 手动删除一个指定的 Block。
    /// </summary>
    /// <param name="blockId">要删除的 Block ID。</param>
    /// <param name="recursive">是否递归删除子 Block。默认递归删除，非递归可能导致奇奇怪怪的问题？</param>
    /// <param name="force">是否强制删除，无视状态。</param>
    /// <returns>删除操作的结果的 HTTP 状态码。</returns>
    /// <response code="200">操作已成功执行。</response>
    /// <response code="400">不允许删除根节点或请求无效（例如 Block 有子节点但未指定 recursive=true）。</response>
    /// <response code="404">未找到具有指定 ID 的 Block。</response>
    /// <response code="409">Block的当前状态不允许删除。现在只允许删除Idle或Error，说实在的有点意义不明，也许以后设计成什么都可以删除。</response>
    /// <response code="500">执行操作时发生内部服务器错误。</response>
    [HttpDelete("{blockId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)] // For InvalidState
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteBlockManually(string blockId, [FromQuery] bool recursive = true,
        [FromQuery] bool force = false)
    {
        var result = await this.blockManagementService.DeleteBlockManuallyAsync(blockId, recursive, force);
        switch (result)
        {
            case ManagementResult.Success:
                return this.Ok();
            case ManagementResult.NotFound:
                return this.NotFound($"Block '{blockId}' 未找到。");
            case ManagementResult.CannotPerformOnRoot:
                return this.BadRequest("不允许删除根节点。");
            case ManagementResult.BadRequest:
                return this.BadRequest("请求无效（例如 Block 有子节点但未指定 recursive=true）。");
            case ManagementResult.CyclicOperation:
                return this.BadRequest("循环移动操作。");
            case ManagementResult.InvalidState:
                return this.Conflict($"Block '{blockId}' 当前状态不允许删除（可尝试 force=true）。");
            case ManagementResult.Error:
                return this.StatusCode(StatusCodes.Status500InternalServerError, "删除 Block 时发生错误。");
            default:
                return this.StatusCode(StatusCodes.Status500InternalServerError, "未知的删除结果。");
        }
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
    [ApiExplorerSettings(GroupName = InternalApiGroupName)]
    [Tags("__DEBUG__")]
    public async Task<IActionResult> MoveBlockManually(string blockId, [FromBody] MoveBlockRequestDto request)
    {
        if (!this.ModelState.IsValid) return this.BadRequest(this.ModelState);

        if (blockId == request.NewParentBlockId) // 基本的循环检查
            return this.Conflict("不能将 Block 移动到自身之下。");

        var result = await this.blockManagementService.MoveBlockManuallyAsync(blockId, request.NewParentBlockId);

        return result switch
        {
            ManagementResult.Success => this.NoContent(),
            ManagementResult.NotFound => this.NotFound(
                $"要移动的 Block '{blockId}' 或目标父 Block '{request.NewParentBlockId}' 未找到。"),
            ManagementResult.CannotPerformOnRoot => this.BadRequest("不允许移动根节点。"),
            ManagementResult.InvalidState => this.Conflict("Block 或目标父 Block 当前状态不允许移动。"),
            ManagementResult.CyclicOperation => this.Conflict("不能将 Block 移动到其自身或其子孙节点下。"),
            ManagementResult.BadRequest => this.BadRequest("请求无效。"),
            ManagementResult.Error => this.StatusCode(StatusCodes.Status500InternalServerError, "移动 Block 时发生错误。"),
            _ => this.StatusCode(StatusCodes.Status500InternalServerError, "未知的移动结果。")
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

// --- END OF FILE BlockManagementController.cs ---