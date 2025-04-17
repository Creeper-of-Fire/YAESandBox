// --- START OF FILE YAESandBox.Tests/API/Controllers/EntitiesControllerTests.cs ---

using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using YAESandBox.API.Controllers;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.Core.State.Entity; // For BaseEntity, EntityType, TypedID, Character etc.

namespace YAESandBox.Tests.API.Controllers;

public class EntitiesControllerTests
{
    private readonly Mock<IBlockReadService> _mockReadService;
    private readonly Mock<IBlockWritService> _mockWritService; // 构造函数需要
    private readonly EntitiesController _controller;

    public EntitiesControllerTests()
    {
        _mockReadService = new Mock<IBlockReadService>();
        _mockWritService = new Mock<IBlockWritService>();
        _controller = new EntitiesController(_mockWritService.Object, _mockReadService.Object);
    }

    [Fact]
    public async Task GetAllEntities_当Block存在且有实体时_应返回实体摘要列表的OkResult()
    {
        // Arrange
        var blockId = "validBlock";
        var entities = new List<BaseEntity>
        {
            new Character("char1") { IsDestroyed = false }, // 使用实际的 Core 实体类型
            new Item("item1") { IsDestroyed = false }
        };
        // 设置 Character 的 name 属性用于测试 DTO 映射
        entities[0].SetAttribute("name", "英雄");

        _mockReadService.Setup(s => s.GetAllEntitiesSummaryAsync(blockId)).ReturnsAsync(entities);

        // Act
        var result = await _controller.GetAllEntities(blockId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<EntitySummaryDto>>().Subject;
        dtos.Should().HaveCount(2);
        dtos.Should().ContainEquivalentOf(new EntitySummaryDto { EntityId = "char1", EntityType = EntityType.Character, IsDestroyed = false, Name = "英雄" });
        dtos.Should().ContainEquivalentOf(new EntitySummaryDto { EntityId = "item1", EntityType = EntityType.Item, IsDestroyed = false, Name = null }); // Item 没有设置 Name

        _mockReadService.Verify(s => s.GetAllEntitiesSummaryAsync(blockId), Times.Once);
    }

    [Fact]
    public async Task GetAllEntities_当Block不存在或无权访问时_应返回NotFoundResult()
    {
        // Arrange
        var blockId = "invalidBlock";
        _mockReadService.Setup(s => s.GetAllEntitiesSummaryAsync(blockId)).ReturnsAsync((IEnumerable<BaseEntity>?)null); // 模拟服务返回 null

        // Act
        var result = await _controller.GetAllEntities(blockId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be($"Block with ID '{blockId}' not found or inaccessible.");
        _mockReadService.Verify(s => s.GetAllEntitiesSummaryAsync(blockId), Times.Once);
    }

     [Fact]
    public async Task GetAllEntities_当Block存在但无实体时_应返回空列表的OkResult()
    {
        // Arrange
        var blockId = "emptyBlock";
        var entities = new List<BaseEntity>(); // 空列表
        _mockReadService.Setup(s => s.GetAllEntitiesSummaryAsync(blockId)).ReturnsAsync(entities);

        // Act
        var result = await _controller.GetAllEntities(blockId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<EntitySummaryDto>>().Subject;
        dtos.Should().BeEmpty(); // 验证列表为空

        _mockReadService.Verify(s => s.GetAllEntitiesSummaryAsync(blockId), Times.Once);
    }


    [Fact]
    public async Task GetEntityDetail_当实体存在时_应返回实体详情的OkResult()
    {
        // Arrange
        var blockId = "validBlock";
        var entityType = EntityType.Character;
        var entityId = "char1";
        var typedId = new TypedID(entityType, entityId);
        var entity = new Character(entityId) { IsDestroyed = false };
        entity.SetAttribute("name", "战士");
        entity.SetAttribute("hp", 100);
        _mockReadService.Setup(s => s.GetEntityDetailAsync(blockId, typedId)).ReturnsAsync(entity);

        // Act
        var result = await _controller.GetEntityDetail(entityType, entityId, blockId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<EntityDetailDto>().Subject;
        dto.EntityId.Should().Be(entityId);
        dto.EntityType.Should().Be(entityType);
        dto.IsDestroyed.Should().BeFalse();
        dto.Name.Should().Be("战士");
        dto.Attributes.Should().ContainKey("hp").WhoseValue.Should().Be(100);
        dto.Attributes.Should().ContainKey("EntityId").WhoseValue.Should().Be(entityId); // GetAllAttributes 包含核心属性
        dto.Attributes.Should().ContainKey("EntityType").WhoseValue.Should().Be(entityType);
        dto.Attributes.Should().ContainKey("IsDestroyed").WhoseValue.Should().Be(false);

        _mockReadService.Verify(s => s.GetEntityDetailAsync(blockId, typedId), Times.Once);
    }

    [Fact]
    public async Task GetEntityDetail_当Block或实体不存在时_应返回NotFoundResult()
    {
        // Arrange
        var blockId = "validBlock";
        var entityType = EntityType.Place;
        var entityId = "nonExistentPlace";
        var typedId = new TypedID(entityType, entityId);
        _mockReadService.Setup(s => s.GetEntityDetailAsync(blockId, typedId)).ReturnsAsync((BaseEntity?)null); // 模拟未找到

        // Act
        var result = await _controller.GetEntityDetail(entityType, entityId, blockId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be($"Entity '{typedId}' not found in block '{blockId}' or block not found.");
        _mockReadService.Verify(s => s.GetEntityDetailAsync(blockId, typedId), Times.Once);
    }
}
// --- END OF FILE YAESandBox.Tests/API/Controllers/EntitiesControllerTests.cs ---