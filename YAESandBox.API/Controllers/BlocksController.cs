using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services; // Assuming BlockManager is a service

namespace YAESandBox.API.Controllers;

[ApiController]
[Route("api/[controller]")] // /api/blocks
public class BlocksController(BlockManager blockManager) : ControllerBase
{
    private BlockManager blockManager { get; } = blockManager;

    /// <summary>
    /// 获取 Block 树的摘要信息（例如用于概览）。
    /// 可以添加分页、过滤等参数。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BlockSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlocks() // 参数: [FromQuery] int page = 1, [FromQuery] int pageSize = 20
    {
        // 调用 BlockManager 获取数据
        var summaries = await this.blockManager.GetAllBlockSummariesAsync(); // 实现这个方法
        return Ok(summaries);
    }

    /// <summary>
    /// 获取单个 Block 的详细信息（不含 WorldState）。
    /// </summary>
    [HttpGet("{blockId}")]
    [ProducesResponseType(typeof(BlockDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlockDetail(string blockId)
    {
        var detail = await this.blockManager.GetBlockDetailDtoAsync(blockId); // 实现这个方法
        if (detail == null)
        {
            return NotFound($"Block with ID '{blockId}' not found.");
        }
        return Ok(detail);
    }

    /// <summary>
    /// 更新父 Block 当前选择的子节点索引。
    /// </summary>
    [HttpPatch("{blockId}/select_child")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // Changed from 400, now explicitly handles invalid index
    public async Task<IActionResult> SelectChild(string blockId, [FromBody] SelectChildRequestDto request)
    {
        var (result, message) = await this.blockManager.SelectChildBlockAsync(blockId, request.SelectedChildIndex);

        return result switch
        {
            UpdateResult.Success => NoContent(), // 成功，返回 204
            UpdateResult.NotFound => NotFound(message), // Block 未找到，返回 404 和消息
            UpdateResult.InvalidOperation => BadRequest(message), // 索引无效，返回 400 和消息
            // 可以处理其他 BlockManager 可能返回的 UpdateResult 枚举成员
            _ => StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.") // 其他未知错误
        };
    }
}