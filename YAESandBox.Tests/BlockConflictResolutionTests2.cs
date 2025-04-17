using FluentAssertions;
using System.Collections.Generic;
using Xunit;
using YAESandBox.Core.Action; // 引入包含 AtomicOperation, OperationResult 的命名空间
using YAESandBox.Core.State; // 引入包含 WorldState, GameState 的命名空间
using YAESandBox.Core.State.Entity; // 引入包含 EntityType, TypedID 的命名空间
using YAESandBox.Core.Block; // 引入包含 Block, BlockStatus 等的命名空间

namespace YAESandBox.Tests.Core.Blocks;

public class BlockConflictDetectionTests
{
    // --- 辅助方法和常量 ---

    // 创建一个用于测试的 Block 实例
    private static YAESandBox.Core.Block.Block CreateTestBlock(string blockId = "test_block")
    {
        // DetectAndHandleConflicts 不依赖 Block 的内部状态（如 WorldState）
        // 它只处理传入的命令列表，因此一个最小化的 Block 实例即可
        var dummyWs = new WorldState();
        var dummyGs = new GameState();
        // 使用内部方法或模拟来创建 Block 对象，这里我们假设有方法可以获取到底层 Block
        // 或者直接调用静态方法创建再获取 Block
        var loadingStatus = YAESandBox.Core.Block.Block.CreateBlock(blockId, null, dummyWs, dummyGs);
        return loadingStatus.Block; // 直接获取 Block 实例
    }

    // 定义一些常用的实体 ID 和属性键
    private const string CharId1 = "char_hero";
    private const string CharId2 = "char_npc";
    private const string ItemId1 = "item_sword";
    private const string AttrName = "name";
    private const string AttrHp = "hp";
    private const string AttrInventory = "inventory";


    // --- 测试用例 ---

    [Fact]
    public void DetectAndHandleConflicts_当无任何操作时_应无冲突()
    {
        // Arrange
        var block = CreateTestBlock();
        var aiCommands = new List<AtomicOperation>();
        var userCommands = new List<AtomicOperation>();

        // Act
        var result = block.DetectAndHandleConflicts(userCommands, aiCommands);

        // Assert
        result.hasBlockingConflict.Should().BeFalse("因为没有任何操作");
        result.resolvedAiCommands.Should().BeEmpty();
        result.resolvedUserCommands.Should().BeEmpty();
        result.conflictingAiForResolution.Should().BeNull();
        result.conflictingUserForResolution.Should().BeNull();
    }

    [Fact]
    public void DetectAndHandleConflicts_当AI和用户操作不同实体时_应无冲突()
    {
        // Arrange
        var block = CreateTestBlock();
        var aiCommands = new List<AtomicOperation>
        {
            AtomicOperation.Create(EntityType.Character, CharId1, new() {{AttrName, "AI Hero"}})
        };
        var userCommands = new List<AtomicOperation>
        {
            AtomicOperation.Create(EntityType.Item, ItemId1, new() {{AttrName, "User Sword"}})
        };

        // Act
        var result = block.DetectAndHandleConflicts(userCommands, aiCommands);

        // Assert
        result.hasBlockingConflict.Should().BeFalse("因为操作的是不同实体");
        result.resolvedAiCommands.Should().BeEquivalentTo(aiCommands); // 内容应一致
        result.resolvedUserCommands.Should().BeEquivalentTo(userCommands); // 内容应一致
        result.conflictingAiForResolution.Should().BeNull();
        result.conflictingUserForResolution.Should().BeNull();
    }

    [Fact]
    public void DetectAndHandleConflicts_当AI和用户修改同一实体的不同属性时_应无冲突()
    {
        // Arrange
        var block = CreateTestBlock();
        var aiCommands = new List<AtomicOperation>
        {
            AtomicOperation.Modify(EntityType.Character, CharId1, AttrHp, Operator.Equal, 100)
        };
        var userCommands = new List<AtomicOperation>
        {
            AtomicOperation.Modify(EntityType.Character, CharId1, AttrName, Operator.Equal, "User Hero")
        };

        // Act
        var result = block.DetectAndHandleConflicts(userCommands, aiCommands);

        // Assert
        result.hasBlockingConflict.Should().BeFalse("因为修改的是同一实体的不同属性");
        result.resolvedAiCommands.Should().BeEquivalentTo(aiCommands);
        result.resolvedUserCommands.Should().BeEquivalentTo(userCommands);
        result.conflictingAiForResolution.Should().BeNull();
        result.conflictingUserForResolution.Should().BeNull();
    }

    [Fact]
    public void DetectAndHandleConflicts_当AI和用户创建相同实体时_应自动重命名用户命令且无阻塞冲突()
    {
        // Arrange
        var block = CreateTestBlock();
        var aiCreateOp = AtomicOperation.Create(EntityType.Character, CharId1, new() {{AttrName, "AI Hero"}});
        var userCreateOp = AtomicOperation.Create(EntityType.Character, CharId1, new() {{AttrName, "User Hero"}});
        var userModifyOpOriginal = AtomicOperation.Modify(EntityType.Character, CharId1, AttrHp, Operator.Equal, 50); // 用户后续修改

        var aiCommands = new List<AtomicOperation> { aiCreateOp };
        var userCommands = new List<AtomicOperation> { userCreateOp, userModifyOpOriginal };

        // Act
        var result = block.DetectAndHandleConflicts(userCommands, aiCommands);

        // Assert
        result.hasBlockingConflict.Should().BeFalse("因为 Create/Create 冲突会自动解决");
        result.resolvedAiCommands.Should().BeEquivalentTo(aiCommands); // AI 命令不变

        // 验证用户命令被重命名
        result.resolvedUserCommands.Should().HaveCount(2);
        // 1. 验证 Create 操作被重命名
        var resolvedUserCreateOp = result.resolvedUserCommands.First(op => op.OperationType == AtomicOperationType.CreateEntity);
        resolvedUserCreateOp.EntityType.Should().Be(EntityType.Character);
        resolvedUserCreateOp.EntityId.Should().NotBe(CharId1, "用户的 Create 操作应该被重命名");
        resolvedUserCreateOp.EntityId.Should().StartWith(CharId1, "重命名的 ID 应该基于原 ID");
        resolvedUserCreateOp.InitialAttributes.Should().BeEquivalentTo(userCreateOp.InitialAttributes); // 属性应保留

        // 2. 验证 Modify 操作的目标 ID 也被更新为新的 ID
        var resolvedUserModifyOp = result.resolvedUserCommands.First(op => op.OperationType == AtomicOperationType.ModifyEntity);
        resolvedUserModifyOp.EntityType.Should().Be(EntityType.Character);
        resolvedUserModifyOp.EntityId.Should().Be(resolvedUserCreateOp.EntityId, "后续操作应指向重命名后的实体 ID");
        resolvedUserModifyOp.AttributeKey.Should().Be(AttrHp);
        resolvedUserModifyOp.ModifyValue.Should().Be(50);

        result.conflictingAiForResolution.Should().BeNull();
        result.conflictingUserForResolution.Should().BeNull();
    }


    [Fact]
    public void DetectAndHandleConflicts_当AI和用户修改同一实体的同一属性时_应是阻塞冲突()
    {
        // Arrange
        var block = CreateTestBlock();
        var aiModifyOp = AtomicOperation.Modify(EntityType.Character, CharId1, AttrHp, Operator.Equal, 100);
        var userModifyOp = AtomicOperation.Modify(EntityType.Character, CharId1, AttrHp, Operator.Equal, 90);

        var aiCommands = new List<AtomicOperation> { aiModifyOp };
        var userCommands = new List<AtomicOperation> { userModifyOp };

        // Act
        var result = block.DetectAndHandleConflicts(userCommands, aiCommands);

        // Assert
        result.hasBlockingConflict.Should().BeTrue("因为修改了同一实体的同一属性");
        result.resolvedAiCommands.Should().BeEquivalentTo(aiCommands); // 即使冲突，也返回原始列表
        result.resolvedUserCommands.Should().BeEquivalentTo(userCommands); // 用户命令在此场景未被修改

        // 验证冲突列表包含正确的命令
        result.conflictingAiForResolution.Should().NotBeNull().And.ContainSingle()
            .Which.Should().BeEquivalentTo(aiModifyOp);
        result.conflictingUserForResolution.Should().NotBeNull().And.ContainSingle()
            .Which.Should().BeEquivalentTo(userModifyOp);
    }

    [Fact]
    public void DetectAndHandleConflicts_当AI创建实体而用户修改该实体时_应无冲突()
    {
        // Arrange (这个场景根据之前的讨论不处理，但写个测试确认行为)
        var block = CreateTestBlock();
        var aiCreateOp = AtomicOperation.Create(EntityType.Character, CharId1);
        var userModifyOp = AtomicOperation.Modify(EntityType.Character, CharId1, AttrName, Operator.Equal, "User Value");

        var aiCommands = new List<AtomicOperation> { aiCreateOp };
        var userCommands = new List<AtomicOperation> { userModifyOp };

        // Act
        var result = block.DetectAndHandleConflicts(userCommands, aiCommands);

        // Assert
        result.hasBlockingConflict.Should().BeFalse("因为 Create/Modify 组合不视为冲突");
        result.resolvedAiCommands.Should().BeEquivalentTo(aiCommands);
        result.resolvedUserCommands.Should().BeEquivalentTo(userCommands);
        result.conflictingAiForResolution.Should().BeNull();
        result.conflictingUserForResolution.Should().BeNull();
    }

    [Fact]
    public void DetectAndHandleConflicts_当AI修改实体而用户删除该实体时_应无冲突()
    {
        // Arrange
        var block = CreateTestBlock();
        var aiModifyOp = AtomicOperation.Modify(EntityType.Character, CharId1, AttrHp, Operator.Equal, 100);
        var userDeleteOp = AtomicOperation.Delete(EntityType.Character, CharId1);

        var aiCommands = new List<AtomicOperation> { aiModifyOp };
        var userCommands = new List<AtomicOperation> { userDeleteOp };

        // Act
        var result = block.DetectAndHandleConflicts(userCommands, aiCommands);

        // Assert
        result.hasBlockingConflict.Should().BeFalse("因为涉及 Delete 的操作不视为冲突");
        result.resolvedAiCommands.Should().BeEquivalentTo(aiCommands);
        result.resolvedUserCommands.Should().BeEquivalentTo(userCommands);
        result.conflictingAiForResolution.Should().BeNull();
        result.conflictingUserForResolution.Should().BeNull();
    }

    [Fact]
    public void DetectAndHandleConflicts_当AI和用户都删除同一实体时_应无冲突()
    {
        // Arrange
        var block = CreateTestBlock();
        var aiDeleteOp = AtomicOperation.Delete(EntityType.Character, CharId1);
        var userDeleteOp = AtomicOperation.Delete(EntityType.Character, CharId1);

        var aiCommands = new List<AtomicOperation> { aiDeleteOp };
        var userCommands = new List<AtomicOperation> { userDeleteOp };

        // Act
        var result = block.DetectAndHandleConflicts(userCommands, aiCommands);

        // Assert
        result.hasBlockingConflict.Should().BeFalse("因为 Delete/Delete 不视为冲突");
        result.resolvedAiCommands.Should().BeEquivalentTo(aiCommands);
        result.resolvedUserCommands.Should().BeEquivalentTo(userCommands);
        result.conflictingAiForResolution.Should().BeNull();
        result.conflictingUserForResolution.Should().BeNull();
    }

    [Fact]
    public void DetectAndHandleConflicts_复杂场景_包含CreateCreate和ModifyModify冲突_应正确处理()
    {
        // Arrange
        var block = CreateTestBlock();

        // AI: 创建 Hero, 修改 NPC HP
        var aiCreateHero = AtomicOperation.Create(EntityType.Character, CharId1, new() {{AttrName, "AI Hero"}});
        var aiModifyNpcHp = AtomicOperation.Modify(EntityType.Character, CharId2, AttrHp, Operator.Equal, 200);
        var aiCommands = new List<AtomicOperation> { aiCreateHero, aiModifyNpcHp };

        // User: 创建 Hero (冲突), 修改 Hero Name (应重命名), 修改 NPC HP (冲突)
        var userCreateHero = AtomicOperation.Create(EntityType.Character, CharId1, new() {{AttrName, "User Hero"}});
        var userModifyHeroNameOriginal = AtomicOperation.Modify(EntityType.Character, CharId1, AttrName, Operator.Equal, "User Renamed?");
        var userModifyNpcHp = AtomicOperation.Modify(EntityType.Character, CharId2, AttrHp, Operator.Equal, 180);
        var userCommands = new List<AtomicOperation> { userCreateHero, userModifyHeroNameOriginal, userModifyNpcHp };

        // Act
        var result = block.DetectAndHandleConflicts(userCommands, aiCommands);

        // Assert
        // 1. 应该检测到阻塞性冲突 (Modify/Modify NPC HP)
        result.hasBlockingConflict.Should().BeTrue("因为 NPC 的 HP 被同时修改");

        // 2. AI 命令不变
        result.resolvedAiCommands.Should().BeEquivalentTo(aiCommands);

        // 3. 用户命令处理: Create Hero 和 Modify Hero Name 应被重命名, Modify NPC HP 不变
        result.resolvedUserCommands.Should().HaveCount(3);
        // 验证重命名
        var resolvedUserCreateOp = result.resolvedUserCommands.First(op => op.OperationType == AtomicOperationType.CreateEntity);
        resolvedUserCreateOp.EntityId.Should().NotBe(CharId1);
        resolvedUserCreateOp.EntityType.Should().Be(EntityType.Character);
        var resolvedUserModifyNameOp = result.resolvedUserCommands.First(op => op.AttributeKey == AttrName);
        resolvedUserModifyNameOp.EntityId.Should().Be(resolvedUserCreateOp.EntityId); // 指向新 ID
        resolvedUserModifyNameOp.EntityType.Should().Be(EntityType.Character);
        // 验证未被重命名的冲突操作
        var resolvedUserModifyNpcHp = result.resolvedUserCommands.First(op => op.AttributeKey == AttrHp);
        resolvedUserModifyNpcHp.EntityId.Should().Be(CharId2); // ID 不变
        resolvedUserModifyNpcHp.EntityType.Should().Be(EntityType.Character);


        // 4. 验证冲突列表只包含 Modify/Modify NPC HP 的冲突
        result.conflictingAiForResolution.Should().NotBeNull().And.ContainSingle()
            .Which.Should().BeEquivalentTo(aiModifyNpcHp); // AI 修改 NPC HP 的操作
        result.conflictingUserForResolution.Should().NotBeNull().And.ContainSingle()
            .Which.EntityId.Should().Be(CharId2); // 确保是用户修改 NPC HP 的操作
        result.conflictingUserForResolution.Single().AttributeKey.Should().Be(AttrHp);
        result.conflictingUserForResolution.Single().OperationType.Should().Be(AtomicOperationType.ModifyEntity);

    }
     [Fact]
    public void DetectAndHandleConflicts_当用户重命名后修改的属性与AI修改的属性相同时_应无冲突()
    {
        // Arrange
        var block = CreateTestBlock();

        // AI: 创建 CharId1, 修改 CharId1 的 HP
        var aiCreateOp = AtomicOperation.Create(EntityType.Character, CharId1);
        var aiModifyOp = AtomicOperation.Modify(EntityType.Character, CharId1, AttrHp, Operator.Equal, 100);
        var aiCommands = new List<AtomicOperation> { aiCreateOp, aiModifyOp };

        // User: 创建 CharId1 (将触发重命名), 修改 *重命名后* 实体的 HP
        var userCreateOp = AtomicOperation.Create(EntityType.Character, CharId1);
        // 这个操作的目标 ID 在重命名后会改变，因此不应与 AI 修改原始 CharId1 的 HP 冲突
        var userModifyOpOriginal = AtomicOperation.Modify(EntityType.Character, CharId1, AttrHp, Operator.Equal, 50);
        var userCommands = new List<AtomicOperation> { userCreateOp, userModifyOpOriginal };

        // Act
        var result = block.DetectAndHandleConflicts(userCommands, aiCommands);

        // Assert
        result.hasBlockingConflict.Should().BeFalse("因为用户的修改是在重命名后的实体上，与AI修改的原始实体不同");

        // 验证AI命令不变
        result.resolvedAiCommands.Should().BeEquivalentTo(aiCommands);

        // 验证用户命令已重命名
        result.resolvedUserCommands.Should().HaveCount(2);
        var resolvedUserCreate = result.resolvedUserCommands.First(op => op.OperationType == AtomicOperationType.CreateEntity);
        var resolvedUserModify = result.resolvedUserCommands.First(op => op.OperationType == AtomicOperationType.ModifyEntity);
        resolvedUserCreate.EntityId.Should().NotBe(CharId1);
        resolvedUserModify.EntityId.Should().Be(resolvedUserCreate.EntityId);
        resolvedUserModify.AttributeKey.Should().Be(AttrHp); // 属性是 HP

        // 验证无冲突报告
        result.conflictingAiForResolution.Should().BeNull();
        result.conflictingUserForResolution.Should().BeNull();
    }
}