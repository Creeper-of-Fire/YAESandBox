using Xunit;
using FluentAssertions;
using NSubstitute;
using YAESandBox.API.Services;
using YAESandBox.API.DTOs;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static YAESandBox.Core.Block.BlockTopologyExporter; // 引入静态成员

namespace YAESandBox.Tests.API.Services;

public class BlockReadServiceTests
{
    private readonly INotifierService _notifierServiceMock;
    private readonly IBlockManager _blockManagerMock; // 直接模拟 BlockManager，因为服务依赖它
    private readonly BlockReadService _sut; // System Under Test - 被测系统

    public BlockReadServiceTests()
    {
        this._notifierServiceMock = Substitute.For<INotifierService>();
        // 注意：BlockManager 是具体类，但我们可以模拟其公共/内部可访问的方法
        // 或者，如果 BlockManager 实现了接口，则模拟接口更好。
        // 这里我们假设需要模拟 BlockManager 本身（或其相关方法）。
        // NSubstitute 可以模拟非密封类的虚方法和接口方法。
        // 如果要模拟非虚方法，可能需要更高级的技术或重构 BlockManager。
        // 为简单起见，我们假设 GetBlockAsync, GetBlocks, GetNodeOnlyBlocks 是可模拟的（例如 virtual 或通过接口）
        // 如果 BlockManager 不易模拟，这表明 BlockManager 可能需要重构以提高可测试性 (例如，提取接口)
        // *** 暂时直接 new 一个，然后在需要的地方 Mock 特定方法的返回值 ***
        this._blockManagerMock = Substitute.For<IBlockManager>(); // 使用 NSubstitute 模拟

        this._sut = new BlockReadService(this._notifierServiceMock, this._blockManagerMock);
    }

    // --- 测试 GetBlockDetailDtoAsync ---

    [Fact]
    public async Task GetBlockDetailDtoAsync_当Block存在时_应返回详细信息Dto()
    {
        // Arrange (准备)
        var blockId = "test-block-1";
        var parentId = BlockManager.WorldRootId;
        var gameState = new GameState();
        var worldState = new WorldState();
        // 使用 Block.CreateBlock 或 Block.CreateBlockFromSave 来创建实例
        var block = Block.CreateBlock(blockId, parentId, worldState, gameState).ForceIdleState(); // 创建一个 Idle 状态的 BlockStatus
        block.Block.BlockContent = "测试内容";
        block.Block.AddOrSetMetaData("TestKey", "TestValue");

        this._blockManagerMock.GetBlockAsync(blockId).Returns(Task.FromResult<BlockStatus?>(block)); // 模拟 GetBlockAsync

        // Act (执行)
        var result = await this._sut.GetBlockDetailDtoAsync(blockId);

        // Assert (断言)
        result.Should().NotBeNull();
        result!.BlockId.Should().Be(blockId);
        result.ParentBlockId.Should().Be(parentId);
        result.StatusCode.Should().Be(BlockStatusCode.Idle);
        result.BlockContent.Should().Be("测试内容");
        result.Metadata.Should().ContainKey("TestKey").WhoseValue.Should().Be("TestValue");
        result.ChildrenInfo.Should().BeEmpty(); // 假设初始没有子节点
    }

    [Fact]
    public async Task GetBlockDetailDtoAsync_当Block不存在时_应返回null()
    {
        // Arrange
        var blockId = "non-existent-block";
        this._blockManagerMock.GetBlockAsync(blockId).Returns(Task.FromResult<BlockStatus?>(null)); // 模拟返回 null

        // Act
        var result = await this._sut.GetBlockDetailDtoAsync(blockId);

        // Assert
        result.Should().BeNull();
    }

    // --- 测试 GetBlockGameStateAsync ---

    [Fact]
    public async Task GetBlockGameStateAsync_当Block存在时_应返回GameState()
    {
        // Arrange
        var blockId = "test-block-gs";
        var gameState = new GameState();
        gameState["level"] = 5;
        var worldState = new WorldState();
        var block = Block.CreateBlock(blockId, null, worldState, gameState).ForceIdleState();
        this._blockManagerMock.GetBlockAsync(blockId).Returns(Task.FromResult<BlockStatus?>(block));

        // Act
        var result = await this._sut.GetBlockGameStateAsync(blockId);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeSameAs(gameState); // 确认不是同一个 GameState 对象，因为内部有Clone
        result!["level"].Should().Be(5);
    }

    [Fact]
    public async Task GetBlockGameStateAsync_当Block不存在时_应返回null()
    {
        // Arrange
        var blockId = "non-existent-block-gs";
        this._blockManagerMock.GetBlockAsync(blockId).Returns(Task.FromResult<BlockStatus?>(null));

        // Act
        var result = await this._sut.GetBlockGameStateAsync(blockId);

        // Assert
        result.Should().BeNull();
    }

    // --- 测试 GetAllEntitiesSummaryAsync ---

    [Fact]
    public async Task GetAllEntitiesSummaryAsync_当Block存在时_应返回非销毁实体列表()
    {
        // Arrange
        var blockId = "test-block-entities";
        var worldState = new WorldState();
        var item1 = new Item("item-1") { IsDestroyed = false };
        item1.SetAttribute("name", "宝剑");
        var char1 = new Character("char-1") { IsDestroyed = false };
        char1.SetAttribute("name", "英雄");
        var place1 = new Place("place-1") { IsDestroyed = true }; // 已销毁
        worldState.AddEntity(item1);
        worldState.AddEntity(char1);
        worldState.AddEntity(place1);

        var block = Block.CreateBlock(blockId, null, worldState, new GameState()).ForceIdleState();
        this._blockManagerMock.GetBlockAsync(blockId).Returns(Task.FromResult<BlockStatus?>(block));

        // Act
        var result = await this._sut.GetAllEntitiesSummaryAsync(blockId);

        // Assert
        result.Should().NotBeNull();
        result!.Should().HaveCount(2); // 只应包含 item-1 和 char-1
        result!.Select(e => e.EntityId).Should().Contain("item-1");
        result!.Select(e => e.EntityId).Should().Contain("char-1");
        result!.Select(e => e.EntityId).Should().NotContain("place-1");
        result!.Should().NotContain(e => e.IsDestroyed);
    }

    [Fact]
    public async Task GetAllEntitiesSummaryAsync_当Block不存在时_应返回null()
    {
        // Arrange
        var blockId = "non-existent-entities";
        this._blockManagerMock.GetBlockAsync(blockId).Returns(Task.FromResult<BlockStatus?>(null));

        // Act
        var result = await this._sut.GetAllEntitiesSummaryAsync(blockId);

        // Assert
        result.Should().BeNull();
    }

    // --- 测试 GetEntityDetailAsync ---

    [Fact]
    public async Task GetEntityDetailAsync_当实体存在时_应返回实体()
    {
        // Arrange
        var blockId = "test-block-entity-detail";
        var entityType = EntityType.Character;
        var entityId = "char-detail";
        var typedId = new TypedID(entityType, entityId);

        var worldState = new WorldState();
        var charDetail = new Character(entityId);
        charDetail.SetAttribute("hp", 100);
        worldState.AddEntity(charDetail);

        var block = Block.CreateBlock(blockId, null, worldState, new GameState()).ForceIdleState();
        this._blockManagerMock.GetBlockAsync(blockId).Returns(Task.FromResult<BlockStatus?>(block));

        // Act
        var result = await this._sut.GetEntityDetailAsync(blockId, typedId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(charDetail); // 确认是同一个实体对象
        result!.EntityId.Should().Be(entityId);
        result.EntityType.Should().Be(entityType);
        result.TryGetAttribute("hp", out int hp).Should().BeTrue();
        hp.Should().Be(100);
    }

    [Fact]
    public async Task GetEntityDetailAsync_当实体不存在时_应返回null()
    {
        // Arrange
        var blockId = "test-block-entity-detail-missing";
        var entityType = EntityType.Item;
        var entityId = "item-missing";
        var typedId = new TypedID(entityType, entityId);

        var worldState = new WorldState(); // 空的 WorldState
        var block = Block.CreateBlock(blockId, null, worldState, new GameState()).ForceIdleState();
        this._blockManagerMock.GetBlockAsync(blockId).Returns(Task.FromResult<BlockStatus?>(block));

        // Act
        var result = await this._sut.GetEntityDetailAsync(blockId, typedId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEntityDetailAsync_当Block不存在时_应返回null()
    {
        // Arrange
        var blockId = "non-existent-entity-detail";
        var typedId = new TypedID(EntityType.Place, "place-1");
        this._blockManagerMock.GetBlockAsync(blockId).Returns(Task.FromResult<BlockStatus?>(null));

        // Act
        var result = await this._sut.GetEntityDetailAsync(blockId, typedId);

        // Assert
        result.Should().BeNull();
    }

    // --- 测试 GetBlockTopologyJsonAsync ---
    [Fact]
    public Task GetBlockTopologyJsonAsync_应调用BlockManager并返回拓扑结构()
    {
        // Arrange
        var rootId = BlockManager.WorldRootId;
        var child1Id = "child-1";
        var child2Id = "child-2";
        var grandchild1Id = "grandchild-1";

        // 创建模拟的 IBlockNode 数据 (可以使用 NSubstitute 创建，或者简单实现)
        var rootNode = Substitute.For<IBlockNode>();
        rootNode.BlockId.Returns(rootId);
        rootNode.ChildrenList.Returns([child1Id, child2Id]); // 注意返回 List<string>

        var child1Node = Substitute.For<IBlockNode>();
        child1Node.BlockId.Returns(child1Id);
        child1Node.ChildrenList.Returns([grandchild1Id]);

        var child2Node = Substitute.For<IBlockNode>();
        child2Node.BlockId.Returns(child2Id);
        child2Node.ChildrenList.Returns([]); // 无子节点

        var grandchild1Node = Substitute.For<IBlockNode>();
        grandchild1Node.BlockId.Returns(grandchild1Id);
        grandchild1Node.ChildrenList.Returns([]);

        var nodes = new Dictionary<string, IBlockNode>
        {
            { rootId, rootNode },
            { child1Id, child1Node },
            { child2Id, child2Node },
            { grandchild1Id, grandchild1Node }
        };
        var readOnlyNodes = nodes.ToDictionary(kv => kv.Key, kv => kv.Value); // 转为 IReadOnlyDictionary

        // 模拟 BlockManager 返回这些节点
        this._blockManagerMock.GetNodeOnlyBlocks().Returns(readOnlyNodes);

        // Act
        var result = this._sut.GetBlockTopologyJsonAsync(); // 注意：原始方法不是 async Task
        var jsonNode = result.Result; // 直接获取结果

        // Assert
        jsonNode.Should().NotBeNull();
        jsonNode!.Id.Should().Be(rootId);
        jsonNode.Children.Should().HaveCount(2);

        var resultChild1 = jsonNode.Children.FirstOrDefault(c => c.Id == child1Id);
        resultChild1.Should().NotBeNull();
        resultChild1!.Children.Should().HaveCount(1);
        resultChild1.Children[0].Id.Should().Be(grandchild1Id);
        resultChild1.Children[0].Children.Should().BeEmpty();

        var resultChild2 = jsonNode.Children.FirstOrDefault(c => c.Id == child2Id);
        resultChild2.Should().NotBeNull();
        resultChild2!.Children.Should().BeEmpty();

        // 验证 BlockManager 的 GetNodeOnlyBlocks 被调用了一次
        this._blockManagerMock.Received(1).GetNodeOnlyBlocks();
        return Task.CompletedTask; // 因为原始方法不是 async, 返回 CompletedTask
    }

    // --- 测试 GetAllBlockDetailsAsync ---
    [Fact]
    public async Task GetAllBlockDetailsAsync_应获取所有Block详情()
    {
        // Arrange
        var blockId1 = "b1";
        var blockId2 = "b2";
        var block1 = Block.CreateBlock(blockId1, null, new WorldState(), new GameState()).ForceIdleState();
        var block2 = Block.CreateBlock(blockId2, blockId1, new WorldState(), new GameState()).ForceIdleState();

        var blocksDict = new Dictionary<string, Block>
        {
            { blockId1, block1.Block },
            { blockId2, block2.Block }
        };
        var readOnlyBlocksDict = blocksDict.ToDictionary(kv => kv.Key, kv => kv.Value); //转为 IReadOnlyDictionary

        this._blockManagerMock.GetBlocks().Returns(readOnlyBlocksDict); // 模拟 GetBlocks
        // 模拟 GetBlockAsync 以便内部调用获取 BlockStatus
        this._blockManagerMock.GetBlockAsync(blockId1).Returns(Task.FromResult<BlockStatus?>(block1));
        this._blockManagerMock.GetBlockAsync(blockId2).Returns(Task.FromResult<BlockStatus?>(block2));

        // Act
        var result = await this._sut.GetAllBlockDetailsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().ContainKey(blockId1);
        result.Should().ContainKey(blockId2);
        result[blockId1].ParentBlockId.Should().BeNull();
        result[blockId2].ParentBlockId.Should().Be(blockId1);
        result[blockId1].StatusCode.Should().Be(BlockStatusCode.Idle);

        // 验证 GetBlocks 被调用一次
        this._blockManagerMock.Received(1).GetBlocks();
        // 验证 GetBlockAsync 被每个 blockId 调用一次
        await this._blockManagerMock.Received(1).GetBlockAsync(blockId1);
        await this._blockManagerMock.Received(1).GetBlockAsync(blockId2);
    }
}