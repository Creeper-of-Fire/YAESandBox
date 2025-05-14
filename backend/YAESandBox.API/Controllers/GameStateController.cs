using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services.InterFaceAndBasic;
using YAESandBox.Depend; // For UpdateResult

namespace YAESandBox.API.Controllers;

/// <summary>
/// 提供与特定 Block 的 GameState 相关的 API 端点。
/// </summary>
[ApiController]
[Route("api/blocks/{blockId}/[controller]")] // /api/blocks/{blockId}/gamestate
public class GameStateController(
    IBlockWritService writServices,
    IBlockReadService readServices)
    : ApiControllerBase(readServices, writServices)
{
    /// <summary>
    /// 获取指定 Block 的当前 GameState。
    /// </summary>
    /// <param name="blockId">目标 Block 的 ID。</param>
    /// <returns>包含 GameState 设置的 DTO。</returns>
    /// <response code="200">成功返回 GameState。</response>
    /// <response code="404">未找到具有指定 ID 的 Block。</response>
    [HttpGet]
    [ProducesResponseType(typeof(GameStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameState(string blockId)
    {
        var gameState = await this.BlockReadService.GetBlockGameStateAsync(blockId); // 实现获取 GameState 的逻辑
        if (gameState == null)
            return this.NotFound($"未找到 ID 为 '{blockId}' 的 Block。");
        // 将 GameState 转换为 DTO
        var dto = new GameStateDto { Settings = new Dictionary<string, object?>(gameState.GetAllSettings()) }; // 创建副本
        return this.Ok(dto);
    }

    /// <summary>
    /// 修改指定 Block 的 GameState。使用 PATCH 方法进行部分更新。
    /// </summary>
    /// <param name="blockId">目标 Block 的 ID。</param>
    /// <param name="request">包含要更新的 GameState 键值对的请求体。</param>
    /// <returns>无内容响应表示成功。</returns>
    /// <response code="204">GameState 更新成功。</response>
    /// <response code="400">请求体无效。</response>
    /// <response code="404">未找到具有指定 ID 的 Block。</response>
    /// <response code="500">更新时发生内部服务器错误。</response>
    [HttpPatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)] // Changed 409 to 500 as per code
    public async Task<IActionResult> UpdateGameState(string blockId, [FromBody] UpdateGameStateRequestDto request)
    {
        // 基本验证
        if (request?.SettingsToUpdate == null)
            return this.BadRequest("请求体或要更新的设置不能为空。");

        var result = await this.BlockWritService.UpdateBlockGameStateAsync(blockId, request.SettingsToUpdate); // 实现更新逻辑

        switch (result)
        {
            case BlockResultCode.Success:

                return this.NoContent(); // 204 No Content
            case BlockResultCode.NotFound:
                return this.NotFound($"未找到 ID 为 '{blockId}' 的 Block。"); // 404 Not Found
            default:
                // 根据 UpdateResult 枚举的其他可能值处理错误，这里假设只有 NotFound
                return this.StatusCode(StatusCodes.Status500InternalServerError, "更新 GameState 时发生意外错误。"); // 500 Internal Server Error
        }
    }
}