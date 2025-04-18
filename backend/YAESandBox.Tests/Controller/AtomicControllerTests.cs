// --- START OF FILE YAESandBox.Tests/API/Controllers/AtomicControllerTests.cs ---

using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;
using YAESandBox.API.Controllers;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.Core.Action;
using YAESandBox.Core.State.Entity;

namespace YAESandBox.Tests.API.Controllers;

public class AtomicControllerTests
{
    private readonly Mock<IBlockWritService> _mockWritService;
    private readonly Mock<IBlockReadService> _mockReadService; // 虽然此类没直接用Read，但构造函数需要
    private readonly AtomicController _controller;

    public AtomicControllerTests()
    {
        this._mockWritService = new Mock<IBlockWritService>();
        this._mockReadService = new Mock<IBlockReadService>(); // 初始化 mock
        this._controller = new AtomicController(this._mockWritService.Object, this._mockReadService.Object);
    }

    // 辅助方法创建有效的请求 DTO
    private BatchAtomicRequestDto CreateValidRequestDto()
    {
        return new BatchAtomicRequestDto
        {
            Operations = new List<AtomicOperationRequestDto>
            {
                new AtomicOperationRequestDto
                {
                    OperationType = "CreateEntity",
                    EntityType = EntityType.Item,
                    EntityId = "newItem"
                }
            }
        };
    }

    [Fact]
    public async Task ExecuteAtomicOperations_当操作成功执行时_应返回OkResult()
    {
        // Arrange
        var blockId = "testBlock";
        var requestDto = this.CreateValidRequestDto();
        // 设置模拟 WritService 的行为
        this._mockWritService.Setup(s => s.EnqueueOrExecuteAtomicOperationsAsync(
                blockId,
                It.IsAny<List<AtomicOperation>>())) // 验证传入 List<AtomicOperation>
            .ReturnsAsync(AtomicExecutionResult.Executed); // 模拟服务返回 Executed

        // Act
        var result = await this._controller.ExecuteAtomicOperations(blockId, requestDto);

        // Assert
        result.Should().BeOfType<OkObjectResult>() // 验证返回类型为 OkObjectResult
            .Which.Value.Should().Be("Operations executed successfully."); // 验证返回的消息

        // 验证 WritService 的方法是否被以正确的参数调用了一次
        this._mockWritService.Verify(s => s.EnqueueOrExecuteAtomicOperationsAsync(
            blockId,
            It.Is<List<AtomicOperation>>(list => list.Count == 1 && list[0].OperationType == AtomicOperationType.CreateEntity)), // 检查传入的操作列表是否符合预期
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAtomicOperations_当操作被排队时_应返回AcceptedResult并包含消息() // 方法名保持不变或更新
    {
        // Arrange
        var blockId = "loadingBlock";
        var requestDto = this.CreateValidRequestDto(); // 假设这个辅助方法是存在的
        this._mockWritService.Setup(s => s.EnqueueOrExecuteAtomicOperationsAsync(blockId, It.IsAny<List<AtomicOperation>>()))
            .ReturnsAsync(AtomicExecutionResult.ExecutedAndQueued); // 模拟服务返回 ExecutedAndQueued

        // Act
        var result = await this._controller.ExecuteAtomicOperations(blockId, requestDto);

        // Assert
        // 验证返回类型为 AcceptedResult (这是正确的类型)
        var acceptedResult = result.Should().BeOfType<AcceptedResult>().Subject;
        // (可选) 验证状态码确实是 202
        acceptedResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        // 验证 Value 属性包含控制器返回的消息 (AcceptedResult 继承自 ObjectResult，有 Value 属性)
        acceptedResult.Value.Should().NotBeNull(); // 先确保 Value 不是 null
        acceptedResult.Value.Should().Be($"Operations executed successfully. Operations queued for block '{blockId}' (loading).");
        // Location 属性应该是 null，因为没有传递 URI 给 Accepted 方法
        acceptedResult.Location.Should().BeNull();

        this._mockWritService.Verify(s => s.EnqueueOrExecuteAtomicOperationsAsync(blockId, It.IsAny<List<AtomicOperation>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAtomicOperations_当Block未找到时_应返回NotFoundResult()
    {
        // Arrange
        var blockId = "nonExistentBlock";
        var requestDto = this.CreateValidRequestDto();
        this._mockWritService.Setup(s => s.EnqueueOrExecuteAtomicOperationsAsync(blockId, It.IsAny<List<AtomicOperation>>()))
            .ReturnsAsync(AtomicExecutionResult.NotFound); // 模拟服务返回 NotFound

        // Act
        var result = await this._controller.ExecuteAtomicOperations(blockId, requestDto);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>() // 验证返回类型为 NotFoundObjectResult
             .Which.Value.Should().Be($"Block with ID '{blockId}' not found."); // 验证错误消息

        this._mockWritService.Verify(s => s.EnqueueOrExecuteAtomicOperationsAsync(blockId, It.IsAny<List<AtomicOperation>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAtomicOperations_当Block处于冲突状态时_应返回ConflictResult()
    {
        // Arrange
        var blockId = "conflictBlock";
        var requestDto = this.CreateValidRequestDto();
        this._mockWritService.Setup(s => s.EnqueueOrExecuteAtomicOperationsAsync(blockId, It.IsAny<List<AtomicOperation>>()))
            .ReturnsAsync(AtomicExecutionResult.ConflictState); // 模拟服务返回 ConflictState

        // Act
        var result = await this._controller.ExecuteAtomicOperations(blockId, requestDto);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>() // 验证返回类型为 ConflictObjectResult
            .Which.Value.Should().Be($"Block '{blockId}' is in a conflict state. Resolve conflict first."); // 验证错误消息

        this._mockWritService.Verify(s => s.EnqueueOrExecuteAtomicOperationsAsync(blockId, It.IsAny<List<AtomicOperation>>()), Times.Once);
    }

     [Fact]
    public async Task ExecuteAtomicOperations_当发生错误时_应返回InternalServerError()
    {
        // Arrange
        var blockId = "errorBlock";
        var requestDto = this.CreateValidRequestDto();
        this._mockWritService.Setup(s => s.EnqueueOrExecuteAtomicOperationsAsync(blockId, It.IsAny<List<AtomicOperation>>()))
            .ReturnsAsync(AtomicExecutionResult.Error); // 模拟服务返回 Error

        // Act
        var result = await this._controller.ExecuteAtomicOperations(blockId, requestDto);

        // Assert
        result.Should().BeOfType<ObjectResult>() // 验证返回类型为 ObjectResult (Status500通常用这个)
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError); // 验证状态码
         result.Should().BeOfType<ObjectResult>()
             .Which.Value.Should().Be("An error occurred during execution."); // 验证错误消息

         this._mockWritService.Verify(s => s.EnqueueOrExecuteAtomicOperationsAsync(blockId, It.IsAny<List<AtomicOperation>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAtomicOperations_当请求无效时_应返回BadRequest()
    {
        // Arrange
        var blockId = "anyBlock";
        // 创建一个无效的请求 DTO (例如，Modify 操作缺少必要字段)
        var invalidRequestDto = new BatchAtomicRequestDto
        {
            Operations = new List<AtomicOperationRequestDto>
            {
                new AtomicOperationRequestDto
                {
                    OperationType = "ModifyEntity", // Modify 类型
                    EntityType = EntityType.Item,
                    EntityId = "someItem",
                    AttributeKey = null // 缺少 AttributeKey
                }
            }
        };
        // 注意：这里的验证逻辑是在控制器内部的 MapToCoreOperations 实现的。
        // 我们不需要模拟 WritService，因为预期在调用它之前就会失败。

        // Act
        var result = await this._controller.ExecuteAtomicOperations(blockId, invalidRequestDto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>() // 验证返回类型为 BadRequestObjectResult
            .Which.Value.Should().Be("Invalid atomic operations provided."); // 验证错误消息

        // 验证 WritService 的方法 *没有* 被调用
        this._mockWritService.Verify(s => s.EnqueueOrExecuteAtomicOperationsAsync(It.IsAny<string>(), It.IsAny<List<AtomicOperation>>()), Times.Never);
    }
}
// --- END OF FILE YAESandBox.Tests/API/Controllers/AtomicControllerTests.cs ---