// --- START OF FILE YAESandBox.Tests/API/Controllers/GameStateControllerTests.cs ---

using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;
using YAESandBox.API.Controllers;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.Core.State; // For GameState, UpdateResult

namespace YAESandBox.Tests.API.Controllers;

public class GameStateControllerTests
{
    private readonly Mock<IBlockWritService> _mockWritService;
    private readonly Mock<IBlockReadService> _mockReadService;
    private readonly GameStateController _controller;

    public GameStateControllerTests()
    {
        this._mockWritService = new Mock<IBlockWritService>();
        this._mockReadService = new Mock<IBlockReadService>();
        this._controller = new GameStateController(this._mockWritService.Object, this._mockReadService.Object);
    }

    [Fact]
    public async Task GetGameState_当Block存在时_应返回GameState的OkResult()
    {
        // Arrange
        var blockId = "testBlock";
        var gameState = new GameState();
        gameState["level"] = 5;
        this._mockReadService.Setup(s => s.GetBlockGameStateAsync(blockId)).ReturnsAsync(gameState);

        // Act
        var result = await this._controller.GetGameState(blockId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<GameStateDto>().Subject;
        dto.Settings.Should().ContainKey("level").WhoseValue.Should().Be(5);
        this._mockReadService.Verify(s => s.GetBlockGameStateAsync(blockId), Times.Once);
    }

    [Fact]
    public async Task GetGameState_当Block不存在时_应返回NotFoundResult()
    {
        // Arrange
        var blockId = "nonExistentBlock";
        this._mockReadService.Setup(s => s.GetBlockGameStateAsync(blockId)).ReturnsAsync((GameState?)null); // 模拟返回 null

        // Act
        var result = await this._controller.GetGameState(blockId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be($"Block with ID '{blockId}' not found.");
        this._mockReadService.Verify(s => s.GetBlockGameStateAsync(blockId), Times.Once);
    }

    [Fact]
    public async Task UpdateGameState_当更新成功时_应返回NoContentResult()
    {
        // Arrange
        var blockId = "testBlock";
        var request = new UpdateGameStateRequestDto
        {
            SettingsToUpdate = new Dictionary<string, object?> { { "score", 100 } }
        };
        this._mockWritService.Setup(s => s.UpdateBlockGameStateAsync(blockId, request.SettingsToUpdate))
            .ReturnsAsync(UpdateResult.Success); // 模拟成功

        // Act
        var result = await this._controller.UpdateGameState(blockId, request);

        // Assert
        result.Should().BeOfType<NoContentResult>(); // 验证返回类型
        this._mockWritService.Verify(s => s.UpdateBlockGameStateAsync(blockId, request.SettingsToUpdate), Times.Once);
    }

    [Fact]
    public async Task UpdateGameState_当Block未找到时_应返回NotFoundResult()
    {
        // Arrange
        var blockId = "nonExistentBlock";
        var request = new UpdateGameStateRequestDto
        {
            SettingsToUpdate = new Dictionary<string, object?> { { "score", 100 } }
        };
        this._mockWritService.Setup(s => s.UpdateBlockGameStateAsync(blockId, request.SettingsToUpdate))
            .ReturnsAsync(UpdateResult.NotFound); // 模拟未找到

        // Act
        var result = await this._controller.UpdateGameState(blockId, request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be($"Block with ID '{blockId}' not found.");
        this._mockWritService.Verify(s => s.UpdateBlockGameStateAsync(blockId, request.SettingsToUpdate), Times.Once);
    }

     [Fact]
    public async Task UpdateGameState_当发生其他错误时_应返回InternalServerError()
    {
        // Arrange
        var blockId = "errorBlock";
        var request = new UpdateGameStateRequestDto
        {
            SettingsToUpdate = new Dictionary<string, object?> { { "score", 100 } }
        };
        // 假设 UpdateResult 有一个 Error 状态，或者服务抛出异常
        this._mockWritService.Setup(s => s.UpdateBlockGameStateAsync(blockId, request.SettingsToUpdate))
             .ReturnsAsync(UpdateResult.InvalidOperation); // 用 InvalidOperation 模拟其他错误

        // Act
        var result = await this._controller.UpdateGameState(blockId, request);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        objectResult.Value.Should().Be("An unexpected error occurred."); // 根据控制器中的错误处理逻辑断言
        this._mockWritService.Verify(s => s.UpdateBlockGameStateAsync(blockId, request.SettingsToUpdate), Times.Once);
    }
}
// --- END OF FILE YAESandBox.Tests/API/Controllers/GameStateControllerTests.cs ---