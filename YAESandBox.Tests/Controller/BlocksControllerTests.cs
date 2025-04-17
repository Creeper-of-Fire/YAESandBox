// --- START OF FILE YAESandBox.Tests/API/Controllers/BlocksControllerTests.cs ---

using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using YAESandBox.API.Controllers;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.Core.Block; // For BlockStatusCode, BlockTopologyExporter
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace YAESandBox.Tests.API.Controllers;

public class BlocksControllerTests
{
    private readonly Mock<IBlockReadService> _mockReadService;
    private readonly Mock<IBlockWritService> _mockWritService; // 虽然此类没直接用Write，但构造函数需要
    private readonly BlocksController _controller;

    public BlocksControllerTests()
    {
        this._mockReadService = new Mock<IBlockReadService>();
        this._mockWritService = new Mock<IBlockWritService>(); // 初始化 mock
        this._controller = new BlocksController(this._mockWritService.Object, this._mockReadService.Object);
    }

    [Fact]
    public async Task GetBlocks_应返回所有Block详情的OkResult()
    {
        // Arrange
        var blockDetails = new Dictionary<string, BlockDetailDto>
        {
            { "blk1", new BlockDetailDto { BlockId = "blk1", StatusCode = BlockStatusCode.Idle } },
            { "blk2", new BlockDetailDto { BlockId = "blk2", StatusCode = BlockStatusCode.Loading } }
        };
        this._mockReadService.Setup(s => s.GetAllBlockDetailsAsync()).ReturnsAsync(blockDetails);

        // Act
        var result = await this._controller.GetBlocks();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(blockDetails); // 比较字典内容
        this._mockReadService.Verify(s => s.GetAllBlockDetailsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetBlockDetail_当Block存在时_应返回Block详情的OkResult()
    {
        // Arrange
        var blockId = "existingBlock";
        var blockDetail = new BlockDetailDto { BlockId = blockId, StatusCode = BlockStatusCode.Idle, BlockContent = "内容" };
        this._mockReadService.Setup(s => s.GetBlockDetailDtoAsync(blockId)).ReturnsAsync(blockDetail);

        // Act
        var result = await this._controller.GetBlockDetail(blockId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(blockDetail); // 可以是 BeSameAs 或 BeEquivalentTo
        this._mockReadService.Verify(s => s.GetBlockDetailDtoAsync(blockId), Times.Once);
    }

    [Fact]
    public async Task GetBlockDetail_当Block不存在时_应返回NotFoundResult()
    {
        // Arrange
        var blockId = "nonExistentBlock";
        this._mockReadService.Setup(s => s.GetBlockDetailDtoAsync(blockId)).ReturnsAsync((BlockDetailDto?)null); // 模拟返回 null

        // Act
        var result = await this._controller.GetBlockDetail(blockId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be($"Block with ID '{blockId}' not found.");
        this._mockReadService.Verify(s => s.GetBlockDetailDtoAsync(blockId), Times.Once);
    }

    [Fact]
    public async Task GetTopology_当服务成功返回拓扑时_应返回包含拓扑的OkResult()
    {
        // Arrange
        var topologyNode = new BlockTopologyExporter.JsonBlockNode("root")
        {
             Children = { new BlockTopologyExporter.JsonBlockNode("child1") }
        };
        // 模拟服务返回 JSON 节点对象
        this._mockReadService.Setup(s => s.GetBlockTopologyJsonAsync()).ReturnsAsync(topologyNode);

        // Act
        var result = await this._controller.GetTopology();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(topologyNode); // 验证返回的是模拟的对象
        okResult.ContentTypes.Should().BeEmpty(); // OkObjectResult 默认不设置 ContentType

        this._mockReadService.Verify(s => s.GetBlockTopologyJsonAsync(), Times.Once);
    }

    [Fact]
    public async Task GetTopology_当服务返回null时_应返回InternalServerError()
    {
        // Arrange
        this._mockReadService.Setup(s => s.GetBlockTopologyJsonAsync()).ReturnsAsync((BlockTopologyExporter.JsonBlockNode?)null);

        // Act
        var result = await this._controller.GetTopology();

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        statusCodeResult.Value.Should().Be("Failed to generate block topology.");
        this._mockReadService.Verify(s => s.GetBlockTopologyJsonAsync(), Times.Once);
    }

    [Fact]
    public async Task GetTopology_当服务抛出异常时_应返回InternalServerError()
    {
        // Arrange
        this._mockReadService.Setup(s => s.GetBlockTopologyJsonAsync()).ThrowsAsync(new Exception("模拟服务层错误"));

        // Act
        var result = await this._controller.GetTopology();

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        statusCodeResult.Value.Should().Be("An unexpected error occurred.");
        this._mockReadService.Verify(s => s.GetBlockTopologyJsonAsync(), Times.Once);
    }
}
// --- END OF FILE YAESandBox.Tests/API/Controllers/BlocksControllerTests.cs ---