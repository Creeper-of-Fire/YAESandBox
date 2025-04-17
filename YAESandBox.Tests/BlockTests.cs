// --- START OF FILE BlockTests.cs ---

using FluentAssertions;
using Xunit;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Core.Action;
using System.Collections.Generic;
using System.Linq;

namespace YAESandBox.Tests.Core.BlockTests;
// Changed namespace slightly

public class BlockTests
{
    private static WorldState CreateInitialWorldState()
    {
        var ws = new WorldState();
        ws.AddEntity(new Character("char-1") { IsDestroyed = false });
        ws.AddEntity(new Place("place-1") { IsDestroyed = false });
        return ws;
    }

    private static GameState CreateInitialGameState()
    {
        var gs = new GameState();
        gs["Difficulty"] = "Normal";
        return gs;
    }

    [Fact]
    public void CreateBlock_ShouldInitializeCorrectlyAndCloneStates()
    {
        // Arrange
        var parentId = "parent-blk";
        var sourceWs = CreateInitialWorldState();
        var sourceGs = CreateInitialGameState();
        var triggerParams = new Dictionary<string, object?> { { "Reason", "Test" } };

        // Act
        var loadingStatus = Block.CreateBlock("new-blk", parentId, sourceWs, sourceGs, triggerParams);
        var block = loadingStatus.Block;

        // Assert
        loadingStatus.StatusCode.Should().Be(BlockStatusCode.Loading);
        block.BlockId.Should().Be("new-blk");
        block.ParentBlockId.Should().Be(parentId);
        block.Metadata.Should().ContainKey("CreationTime");
        block.Metadata.Should().ContainKey("TriggerParams").WhoseValue.Should().BeEquivalentTo(triggerParams);

        // Check Cloned States (not same instance)
        block.wsInput.Should().NotBeSameAs(sourceWs);
        block.GameState.Should().NotBeSameAs(sourceGs);

        // Check Cloned State Content
        block.wsInput.Characters.Should().ContainKey("char-1");
        block.GameState["Difficulty"].Should().Be("Normal");

        // wsTemp should be a clone of wsInput initially
        block.wsTemp.Should().NotBeNull();
        block.wsTemp.Should().NotBeSameAs(block.wsInput);
        block.wsTemp!.Characters.Should().ContainKey("char-1");

        // Other states should be null initially
        block.wsPostAI.Should().BeNull();
        block.wsPostUser.Should().BeNull();
    }

    // --- Test ApplyOperationTo (Static Helper inside Block) ---

    [Fact]
    public void ApplyOperationTo_CreateEntity_ShouldSucceed_WhenNotExists()
    {
        // Arrange
        var ws = CreateInitialWorldState();
        var op = AtomicOperation.Create(EntityType.Item, "item-new", new() { { "name", "New Sword" } });

        // Act
        var result = Block.ApplyOperationsTo(ws, [op]).Single(); // Apply single op

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.OriginalOperation.Should().Be(op);
        ws.FindEntity(new TypedID(EntityType.Item, "item-new")).Should().NotBeNull()
            .And.Subject.As<Item>().GetAttribute("name").Should().Be("New Sword");
    }

    [Fact]
    public void ApplyOperationTo_CreateEntity_ShouldFail_WhenExistsAndNotDestroyed()
    {
        // Arrange
        var ws = CreateInitialWorldState(); // Contains char-1
        var op = AtomicOperation.Create(EntityType.Character, "char-1"); // Try to create existing

        // Act
        var result = Block.ApplyOperationsTo(ws, [op]).Single();

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("已存在");
        result.OriginalOperation.Should().Be(op);
        ws.Characters.Should().HaveCount(1); // No new character added
    }

    [Fact]
    public void ApplyOperationTo_ModifyEntity_ShouldSucceed_WhenExists()
    {
        // Arrange
        var ws = CreateInitialWorldState(); // Contains char-1
        var op = AtomicOperation.Modify(EntityType.Character, "char-1", "HP", Operator.Add, 10);

        // Act
        var result = Block.ApplyOperationsTo(ws, [op]).Single();

        // Assert
        result.Success.Should().BeTrue();
        ws.Characters["char-1"].GetAttribute("HP").Should().Be(10); // Assuming starts null, becomes 10
    }

    [Fact]
    public void ApplyOperationTo_ModifyEntity_ShouldFail_WhenNotExists()
    {
        // Arrange
        var ws = CreateInitialWorldState();
        var op = AtomicOperation.Modify(EntityType.Character, "char-nonexistent", "HP", Operator.Equal, 100);

        // Act
        var result = Block.ApplyOperationsTo(ws, [op]).Single();

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未找到");
    }

    [Fact]
    public void ApplyOperationTo_ModifyEntity_ShouldFail_WhenAttributeOrOperatorInvalid()
    {
        // Arrange
        var ws = CreateInitialWorldState(); // Contains char-1
        var opNoKey = new AtomicOperation
        {
            OperationType = AtomicOperationType.ModifyEntity, EntityType = EntityType.Character, EntityId = "char-1",
            ModifyOperator = Operator.Equal, ModifyValue = 100
        };
        var opNoOp = new AtomicOperation
        {
            OperationType = AtomicOperationType.ModifyEntity, EntityType = EntityType.Character, EntityId = "char-1",
            AttributeKey = "HP", ModifyValue = 100
        };
        var opNullValue =
            AtomicOperation.Modify(EntityType.Character, "char-1", "HP", Operator.Equal,
                null); // Assuming ModifyValue cannot be null

        // Act
        var resultNoKey = Block.ApplyOperationsTo(ws, [opNoKey]).Single();
        var resultNoOp = Block.ApplyOperationsTo(ws, [opNoOp]).Single();
        var resultNullValue = Block.ApplyOperationsTo(ws, [opNullValue]).Single();


        // Assert
        resultNoKey.Success.Should().BeFalse();
        resultNoKey.ErrorMessage.Should().Contain("参数无效");
        resultNoOp.Success.Should().BeFalse();
        resultNoOp.ErrorMessage.Should().Contain("参数无效");
        resultNullValue.Success.Should().BeFalse(); // Based on current implementation
        resultNullValue.ErrorMessage.Should().Contain("值不能为 null");
    }

    [Fact]
    public void ApplyOperationTo_DeleteEntity_ShouldSucceed_AndMarkAsDestroyed()
    {
        // Arrange
        var ws = CreateInitialWorldState(); // Contains char-1
        var op = AtomicOperation.Delete(EntityType.Character, "char-1");

        // Act
        var result = Block.ApplyOperationsTo(ws, [op]).Single();

        // Assert
        result.Success.Should().BeTrue(); // Delete is idempotent
        ws.FindEntity(new TypedID(EntityType.Character, "char-1")).Should()
            .BeNull(); // Find (default) doesn't find destroyed
        ws.FindEntity(new TypedID(EntityType.Character, "char-1"), includeDestroyed: true).Should().NotBeNull()
            .And.Subject.As<Character>().IsDestroyed.Should().BeTrue();
    }

    [Fact]
    public void ApplyOperationTo_DeleteEntity_ShouldSucceed_WhenNotExists()
    {
        // Arrange
        var ws = CreateInitialWorldState();
        var op = AtomicOperation.Delete(EntityType.Item, "item-nonexistent");

        // Act
        var result = Block.ApplyOperationsTo(ws, [op]).Single();

        // Assert
        result.Success.Should().BeTrue(); // Delete is idempotent
    }
    
}
// --- END OF FILE BlockTests.cs ---