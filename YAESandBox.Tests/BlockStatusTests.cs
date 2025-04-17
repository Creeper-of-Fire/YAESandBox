using Xunit;
using FluentAssertions;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.Action;
using YAESandBox.Core.State.Entity;
using OneOf;

namespace YAESandBox.Core.Tests;

/// <summary>
/// 针对 BlockStatus 及其子类的单元测试。
/// </summary>
public class BlockStatusTests
{
    private Block.Block CreateTestBlock(string id = "blk_test", WorldState? inputWs = null, GameState? inputGs = null)
    {
        inputWs ??= new WorldState();
        inputGs ??= new GameState();
        // 使用内部构造函数创建 Block 实例，注意这可能需要调整访问修饰符或使用工厂方法
        // 为了简单起见，我们直接调用静态工厂方法，它返回 LoadingBlockStatus
        var loadingStatus = Block.Block.CreateBlock(id, BlockManager.WorldRootId, inputWs, inputGs);
        return loadingStatus.Block; // 返回内部的 Block 对象
    }

    /// <summary>
    /// 测试 IdleBlockStatus 的 CurrentWorldState 属性。
    /// </summary>
    [Fact]
    public void IdleBlockStatus_CurrentWorldState_应返回wsPostUser()
    {
        // Arrange
        var block = CreateTestBlock();
        block.wsPostUser = new WorldState(); // 确保 wsPostUser 存在
        var status = new IdleBlockStatus(block);
        var expectedWs = block.wsPostUser;

        // Act
        var currentWs = status.CurrentWorldState;

        // Assert
        currentWs.Should().NotBeNull();
        currentWs.Should().BeSameAs(expectedWs); // 确认是同一个实例
    }

    /// <summary>
    /// 测试 LoadingBlockStatus 的 CurrentWorldState 属性。
    /// </summary>
    [Fact]
    public void LoadingBlockStatus_CurrentWorldState_应返回wsTemp()
    {
        // Arrange
        var block = CreateTestBlock();
        var status = new LoadingBlockStatus(block); // 创建时 wsTemp 已基于 wsInput 初始化
        var expectedWs = block.wsTemp;

        // Act
        var currentWs = status.CurrentWorldState;

        // Assert
        currentWs.Should().NotBeNull();
        currentWs.Should().BeSameAs(expectedWs);
    }

    /// <summary>
    /// 测试 ConflictBlockStatus 的 CurrentWorldState 属性。
    /// </summary>
    [Fact]
    public void ConflictBlockStatus_CurrentWorldState_应返回wsTemp()
    {
        // Arrange
        var block = CreateTestBlock();
        block.wsTemp = new WorldState(); // 确保 wsTemp 存在
        var status = new ConflictBlockStatus(block, [], [], [], []);
        var expectedWs = block.wsTemp;

        // Act
        var currentWs = status.CurrentWorldState;

        // Assert
        currentWs.Should().NotBeNull();
        currentWs.Should().BeSameAs(expectedWs);
    }

    /// <summary>
    /// 测试 ErrorBlockStatus 的 CurrentWorldState 属性。
    /// </summary>
    [Fact]
    public void ErrorBlockStatus_CurrentWorldState_应返回wsPostUser()
    {
        // Arrange
        var block = CreateTestBlock();
        block.wsPostUser = new WorldState(); // 确保 wsPostUser 存在
        var status = new ErrorBlockStatus(block);
        var expectedWs = block.wsPostUser;

        // Act
        var currentWs = status.CurrentWorldState;

        // Assert
        currentWs.Should().NotBeNull();
        currentWs.Should().BeSameAs(expectedWs);
    }

    /// <summary>
    /// 测试 IdleBlockStatus 的 ApplyOperations 方法。
    /// </summary>
    [Fact]
    public void IdleBlockStatus_ApplyOperations_应直接修改wsPostUser()
    {
        // Arrange
        var block = CreateTestBlock();
        block.wsPostUser = new WorldState(); // Idle 状态操作 wsPostUser
        var status = new IdleBlockStatus(block);
        var op = AtomicOperation.Create(EntityType.Place, "place_market");
        var operations = new List<AtomicOperation> { op };

        // Act
        var results = status.ApplyOperations(operations);

        // Assert
        results.Should().HaveCount(1).And.OnlyContain(r => r.Success);
        status.CurrentWorldState.FindEntityById("place_market", EntityType.Place).Should().NotBeNull();
        block.wsInput.FindEntityById("place_market", EntityType.Place).Should().BeNull(); // wsInput 不应改变
        block.wsTemp?.FindEntityById("place_market", EntityType.Place).Should().BeNull(); // wsTemp 不应改变
    }

    /// <summary>
    /// 测试 LoadingBlockStatus 的 ApplyOperations 方法。
    /// </summary>
    [Fact]
    public void LoadingBlockStatus_ApplyOperations_应修改wsTemp并暂存命令()
    {
        // Arrange
        var block = CreateTestBlock();
        var status = new LoadingBlockStatus(block); // wsTemp 已初始化
        var op = AtomicOperation.Create(EntityType.Character, "char_merchant");
        var operations = new List<AtomicOperation> { op };

        // Act
        var results = status.ApplyOperations(operations);

        // Assert
        results.Should().HaveCount(1).And.OnlyContain(r => r.Success);

        // 验证 wsTemp 被修改
        status.CurrentWorldState.Should().BeSameAs(block.wsTemp);
        status.CurrentWorldState.FindEntityById("char_merchant", EntityType.Character).Should().NotBeNull();

        // 验证命令被暂存
        status.PendingUserCommands.Should().ContainSingle().Which.Should().BeEquivalentTo(op);

        // 验证其他状态不变
        block.wsInput.FindEntityById("char_merchant", EntityType.Character).Should().BeNull();
        block.wsPostAI.Should().BeNull();
        block.wsPostUser.Should().BeNull();
    }

    /// <summary>
    /// 测试 LoadingBlockStatus 成功完成工作流且无冲突。
    /// </summary>
    [Fact]
    public void LoadingBlockStatus_TryFinalizeSuccessfulWorkflow_无冲突_应返回Idle状态和结果()
    {
        // Arrange
        var block = CreateTestBlock();
        var loadingStatus = new LoadingBlockStatus(block);
        var aiOp = AtomicOperation.Create(EntityType.Item, "item_gem");
        var userOp = AtomicOperation.Create(EntityType.Place, "place_cave");
        loadingStatus.ApplyOperations([userOp]); // 模拟用户操作

        var rawContent = "Workflow finished.";
        var aiCommands = new List<AtomicOperation> { aiOp };

        // Act
        var result = loadingStatus.TryFinalizeSuccessfulWorkflow(rawContent, aiCommands);

        // Assert
        result.IsT0.Should().BeTrue(); // 应该是包含 Idle/Error 和结果的元组
        var (statusUnion, opResults) = result.AsT0;

        statusUnion.IsT0.Should().BeTrue(); // 应该是 Idle
        var idleStatus = statusUnion.AsT0;
        idleStatus.Should().BeOfType<IdleBlockStatus>();

        opResults.Should().HaveCount(2); // AI + User
        opResults.Should().OnlyContain(r => r.Success);

        // 验证 Block 状态
        idleStatus.Block.BlockContent.Should().Be(rawContent);
        idleStatus.Block.wsTemp.Should().BeNull();
        idleStatus.Block.wsPostAI.Should().NotBeNull();
        idleStatus.Block.wsPostUser.Should().NotBeNull();

        // 验证最终状态包含两个操作
        var finalState = idleStatus.CurrentWorldState;
        finalState.FindEntityById("item_gem", EntityType.Item).Should().NotBeNull();
        finalState.FindEntityById("place_cave", EntityType.Place).Should().NotBeNull();

        loadingStatus.PendingUserCommands.Should().BeEmpty(); // 完成后应清空
    }

    /// <summary>
    /// 测试 LoadingBlockStatus 成功完成工作流但有冲突。
    /// </summary>
    [Fact]
    public void LoadingBlockStatus_TryFinalizeSuccessfulWorkflow_有冲突_应返回Conflict状态()
    {
        // Arrange
        var inputWs = new WorldState();
        inputWs.AddEntity(new Item("item_shield"));
        var block = CreateTestBlock(inputWs: inputWs);
        var loadingStatus = new LoadingBlockStatus(block);

        var aiOp = AtomicOperation.Modify(EntityType.Item, "item_shield", "defense", "=", 5);
        var userOp = AtomicOperation.Modify(EntityType.Item, "item_shield", "defense", "=", 3);
        loadingStatus.ApplyOperations([userOp]); // 模拟用户冲突操作

        var rawContent = "Conflict occurred.";
        var aiCommands = new List<AtomicOperation> { aiOp };

        // Act
        var result = loadingStatus.TryFinalizeSuccessfulWorkflow(rawContent, aiCommands);

        // Assert
        result.IsT1.Should().BeTrue(); // 应该是 Conflict 状态
        var conflictStatus = result.AsT1;
        conflictStatus.Should().NotBeNull();
        conflictStatus.Should().BeOfType<ConflictBlockStatus>();

        // 验证 Block 状态
        conflictStatus.Block.BlockContent.Should().Be(rawContent);
        conflictStatus.Block.wsTemp.Should().NotBeNull(); // wsTemp 保留
        conflictStatus.Block.wsPostAI.Should().BeNull();
        conflictStatus.Block.wsPostUser.Should().BeNull();

        // 验证冲突命令
        conflictStatus.conflictingAiCommands.Should().ContainSingle().Which.Should().BeEquivalentTo(aiOp);
        conflictStatus.conflictingUserCommands.Should().ContainSingle().Which.Should().BeEquivalentTo(userOp);
    }

    /// <summary>
    /// 测试 LoadingBlockStatus 工作流完成时，如果应用命令失败，则进入 Error 状态。
    /// </summary>
    [Fact]
    public void LoadingBlockStatus_FinalizeSuccessfulWorkflow_应用命令失败_应返回Error状态()
    {
        // Arrange
        var block = CreateTestBlock();
        var loadingStatus = new LoadingBlockStatus(block);
        // 构造一个会失败的操作：修改一个不存在的实体的属性
        var failingOp = AtomicOperation.Modify(EntityType.Item, "item_nonexistent", "value", "=", 100);

        var rawContent = "Workflow finished, but command failed.";
        var commands = new List<AtomicOperation> { failingOp };

        // Act
        // 直接调用内部方法来测试失败路径
        var (statusUnion, opResults) = loadingStatus._FinalizeSuccessfulWorkflow(rawContent, commands);

        // Assert
        statusUnion.IsT1.Should().BeTrue(); // 应该是 Error 状态
        var errorStatus = statusUnion.AsT1;
        errorStatus.Should().BeOfType<ErrorBlockStatus>();

        opResults.Should().HaveCount(1);
        opResults[0].Success.Should().BeFalse(); // 操作失败
        opResults[0].ErrorMessage.Should().NotBeNullOrWhiteSpace();

        // 验证 Block 状态
        errorStatus.Block.BlockContent.Should().Be(rawContent);
        errorStatus.Block.Metadata.Should().ContainKey("Error");
        errorStatus.Block.wsTemp.Should().BeNull(); // wsTemp 应清除
        errorStatus.Block.wsPostAI.Should().BeNull(); // 失败时不保留 wsPostAI
        errorStatus.Block.wsPostUser.Should().BeNull(); // 失败时不保留 wsPostUser
    }

    /// <summary>
    /// 测试 IdleBlockStatus 创建子节点。
    /// </summary>
    [Fact]
    public void IdleBlockStatus_CreateNewChildrenBlock_应成功创建Loading状态的子节点()
    {
        // Arrange
        var parentBlock = CreateTestBlock("blk_parent");
        parentBlock.wsPostUser = new WorldState(); // Idle 状态需要 wsPostUser
        var parentStatus = new IdleBlockStatus(parentBlock);

        // Act
        var (newBlockId, newChildStatus) = parentStatus.CreateNewChildrenBlock();

        // Assert
        newBlockId.Should().NotBeNullOrWhiteSpace();
        newChildStatus.Should().NotBeNull().And.BeOfType<LoadingBlockStatus>();

        var childBlock = newChildStatus.Block;
        childBlock.BlockId.Should().Be(newBlockId);
        childBlock.ParentBlockId.Should().Be(parentBlock.BlockId);
        childBlock.wsInput.Should().NotBeNull();
        // 确认子节点的 wsInput 是父节点 wsPostUser 的克隆（通过检查是否存在父节点添加的实体来间接验证）
        parentBlock.wsPostUser.AddEntity(new Item("item_from_parent"));
        // 重新创建子节点以获取更新后的 wsInput
        (_, newChildStatus) = parentStatus.CreateNewChildrenBlock();
        newChildStatus.Block.wsInput.FindEntityById("item_from_parent", EntityType.Item).Should().NotBeNull();

        childBlock.wsTemp.Should().NotBeNull(); // Loading 状态有 wsTemp
        childBlock.wsPostAI.Should().BeNull();
        childBlock.wsPostUser.Should().BeNull();
    }
}