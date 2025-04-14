using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;

namespace YAESandBox.API.Controllers;

[ApiController]
[Route("api/blocks/{blockId}/[controller]")] // /api/blocks/{blockId}/gamestate
public class GameStateController : ControllerBase
{
    private readonly BlockManager _blockManager;

    public GameStateController(BlockManager blockManager)
    {
        _blockManager = blockManager;
    }

    /// <summary>
    /// 获取指定 Block 的 GameState。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GameStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameState(string blockId)
    {
        var gameState = await _blockManager.GetBlockGameStateAsync(blockId); // 实现获取 GameState 的逻辑
        if (gameState == null)
        {
            return NotFound($"Block with ID '{blockId}' not found.");
        }
        var dto = new GameStateDto { Settings = (Dictionary<string, object?>)gameState.GetAllSettings() }; // 转换为 DTO
        return Ok(dto);
    }

    /// <summary>
    /// 修改指定 Block 的 GameState。
    /// </summary>
    [HttpPatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)] // If block is loading
    public async Task<IActionResult> UpdateGameState(string blockId, [FromBody] UpdateGameStateRequestDto request)
    {
        var result = await _blockManager.UpdateBlockGameStateAsync(blockId, request.SettingsToUpdate); // 实现更新逻辑

        return result switch
        {
            UpdateResult.Success => NoContent(),
            UpdateResult.NotFound => NotFound($"Block with ID '{blockId}' not found."),
            UpdateResult.Conflict => Conflict($"Block '{blockId}' is currently loading and cannot be modified."),
            _ => StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.") // Or specific error
        };
    }
}

// Helper enum for update results (can be defined elsewhere)
public enum UpdateResult { Success, NotFound, Conflict, InvalidOperation }