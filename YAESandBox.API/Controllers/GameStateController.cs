using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.Core.State;

namespace YAESandBox.API.Controllers;

[ApiController]
[Route("api/blocks/{blockId}/[controller]")] // /api/blocks/{blockId}/gamestate
public class GameStateController(IBlockWritService writServices, IBlockReadService readServices) : ControllerBase
{
    private IBlockWritService BlockWritService { get; } = writServices;
    private IBlockReadService blockReadService { get; } = readServices;

    /// <summary>
    /// 获取指定 Block 的 GameState。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GameStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameState(string blockId)
    {
        var gameState = await this.blockReadService.GetBlockGameStateAsync(blockId); // 实现获取 GameState 的逻辑
        if (gameState == null)
        {
            return this.NotFound($"Block with ID '{blockId}' not found.");
        }
        var dto = new GameStateDto { Settings = (Dictionary<string, object?>)gameState.GetAllSettings() }; // 转换为 DTO
        return this.Ok(dto);
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
        var result = await this.BlockWritService.UpdateBlockGameStateAsync(blockId, request.SettingsToUpdate); // 实现更新逻辑

        return result switch
        {
            UpdateResult.Success => this.NoContent(),
            UpdateResult.NotFound => this.NotFound($"Block with ID '{blockId}' not found."),
            _ => this.StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.") // Or specific error
        };
    }
}