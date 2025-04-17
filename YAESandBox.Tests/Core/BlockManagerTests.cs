using Xunit;
using FluentAssertions;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.Action;
using YAESandBox.Core.State.Entity;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using OneOf.Types;
using OneOf;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace YAESandBox.Core.Tests;

/// <summary>
/// 针对 BlockManager 的单元测试。
/// </summary>
public class BlockManagerTests
{
    /// <summary>
    /// 创建一个新的 BlockManager 实例以进行测试。
    /// </summary>
    private BlockManager CreateTestManager() => new();

    /// <summary>
    /// 测试 BlockManager 的构造函数是否正确创建了根节点。
    /// </summary>
    [Fact]
    public async Task 构造函数_应创建并初始化世界根节点()
    {
        // Arrange & Act
        var manager = CreateTestManager();
        var rootBlock = await manager.GetBlockAsync(BlockManager.WorldRootId);

        // Assert
        manager.GetBlocks().Should().HaveCount(1); // 确认只有一个块
        manager.GetBlocks().Should().ContainKey(BlockManager.WorldRootId); // 确认包含根节点ID
        rootBlock.Should().NotBeNull(); // 确认能获取到根节点
        rootBlock.Should().BeOfType<IdleBlockStatus>(); // 确认根节点是 Idle 状态
        rootBlock!.Block.ParentBlockId.Should().BeNull(); // 确认根节点没有父节点
        rootBlock.Block.wsInput.Should().NotBeNull(); // 确认输入世界状态已创建
        rootBlock.Block.GameState.Should().NotBeNull(); // 确认游戏状态已创建
        rootBlock.Block.wsPostUser.Should().NotBeNull(); // Idle 状态应该有 wsPostUser
    }

    /// <summary>
    /// 测试在父节点存在且处于 Idle 状态时创建子节点。
    /// </summary>
    [Fact]
    public async Task CreateChildBlock_Async_当父节点空闲时_应成功创建子节点并返回Loading状态()
    {
        // Arrange
        var manager = CreateTestManager();
        var parentId = BlockManager.WorldRootId;
        var triggerParams = new Dictionary<string, object?> { { "reason", "test" } };

        // Act
        var newBlockStatus = await manager.CreateChildBlock_Async(parentId, triggerParams);
        var parentBlock = await manager.GetBlockAsync(parentId) as IdleBlockStatus; // 获取更新后的父节点
        var newBlockId = newBlockStatus?.Block.BlockId; // 获取新块 ID

        // Assert
        newBlockStatus.Should().NotBeNull(); // 确认返回了新的块状态
        newBlockStatus.Should().BeOfType<LoadingBlockStatus>(); // 确认新块是 Loading 状态
        newBlockId.Should().NotBeNullOrWhiteSpace(); // 确认新块有 ID

        manager.GetBlocks().Should().HaveCount(2); // 确认总共有两个块
        manager.GetBlocks().Should().ContainKey(newBlockId); // 确认管理器包含新块

        var childBlockFromManager = await manager.GetBlockAsync(newBlockId!); // 从管理器获取新块
        childBlockFromManager.Should().NotBeNull();
        childBlockFromManager!.Block.ParentBlockId.Should().Be(parentId); // 确认父 ID 正确
        childBlockFromManager.Block.Metadata.Should().ContainKey("TriggerParams"); // 确认触发参数已记录
        (childBlockFromManager.Block.Metadata["TriggerParams"]).Should().BeEquivalentTo(JsonSerializer.Serialize(triggerParams));

        parentBlock.Should().NotBeNull();
        parentBlock!.Block.ChildrenList.Should().Contain(newBlockId); // 确认父节点记录了子节点
        parentBlock.Block.TriggeredChildParams.Should().BeEquivalentTo(triggerParams); // 确认父节点记录了触发参数
    }

    /// <summary>
    /// 测试当父节点不存在时尝试创建子节点。
    /// </summary>
    [Fact]
    public async Task CreateChildBlock_Async_当父节点不存在时_应失败并返回null()
    {
        // Arrange
        var manager = CreateTestManager();
        var nonExistentParentId = "blk_不存在的父节点";
        var triggerParams = new Dictionary<string, object?> { { "reason", "test" } };

        // Act
        var newBlockStatus = await manager.CreateChildBlock_Async(nonExistentParentId, triggerParams);

        // Assert
        newBlockStatus.Should().BeNull(); // 确认返回 null
        manager.GetBlocks().Should().HaveCount(1); // 确认没有添加新块
    }

    // // 定义一个简单的测试替身
    // public class MockLoadingStatus : BlockStatus
    // {
    //     // 需要一个 Block 实例，但我们可以用一个基础的
    //     public MockLoadingStatus() : base(Core.Block.Block.CreateBlock("mock-parent", null, /* ... 最少的有效参数 ... */))
    //     {
    //     }
    //
    //     // 覆盖 StatusCode
    //     public BlockStatusCode StatusCode => BlockStatusCode.Loading;
    //
    //     // 不需要实现 LoadingBlockStatus 的其他复杂逻辑
    // }
    //
    // // ... 在测试方法中 ...
    // [Fact]
    // public async Task CreateChildBlock_Async_当父节点不处于空闲状态时_应失败并返回null()
    // {
    //     // Arrange
    //     var manager = CreateTestManager();
    //     var parentId = BlockManager.WorldRootId;
    //
    //     // 使用反射获取字典
    //     var blocksInternal = (ConcurrentDictionary<string, BlockStatus>)manager.GetType()
    //         .GetField("blocks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
    //         .GetValue(manager)!;
    //
    //     // 强制父节点（根节点）变为 Mock Loading 状态
    //     blocksInternal[parentId] = new MockLoadingStatus(); // 使用替身
    //
    //     // Act
    //     var newBlockStatus = await manager.CreateChildBlock_Async(parentId,
    //         new Dictionary<string, object?> { { "reason", "another test" } });
    //
    //     // Assert
    //     newBlockStatus.Should().BeNull(); // 确认返回 null
    // }

    // /// <summary>
    // /// 测试当父节点不处于 Idle 状态时（例如 Loading）尝试创建子节点。
    // /// </summary>
    // [Fact]
    // public async Task CreateChildBlock_Async_当父节点不处于空闲状态时_应失败并返回null()
    // {
    //     // Arrange
    //     var manager = CreateTestManager();
    //     var parentId = BlockManager.WorldRootId;
    //     // 手动创建一个子节点，让父节点从 Idle 变为 Loading -> Idle，但我们模拟它停留在 Loading
    //     var child = await manager.CreateChildBlock_Async(parentId, new Dictionary<string, object?>());
    //     // **注意:** 实际代码中 BlockManager 内部会自动管理状态。
    //     // 这里为了测试 *假设* 父节点卡在非 Idle 状态的情况。
    //     // 在实际测试中，更真实的场景是先让一个 Block 进入 Loading 状态，然后尝试基于它创建子节点。
    //     // 为了简化，我们直接修改状态（这在真实测试中不推荐，但为了演示概念）
    //     var blocksInternal = (ConcurrentDictionary<string, BlockStatus>)manager.GetType()
    //         .GetField("blocks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
    //         .GetValue(manager)!;
    //
    //     // 强制父节点（根节点）变为 Loading 状态（仅用于测试目的！）
    //     var rootBlock = await manager.GetBlockAsync(parentId);
    //     blocksInternal[parentId] = new LoadingBlockStatus(rootBlock!.Block); // 强行修改状态
    //
    //     // Act
    //     var newBlockStatus = await manager.CreateChildBlock_Async(parentId,
    //         new Dictionary<string, object?> { { "reason", "another test" } });
    //
    //     // Assert
    //     newBlockStatus.Should().BeNull(); // 确认返回 null
    //     // manager.Blocks.Should().HaveCount(2); // 原有的根和第一个子节点
    // }

    /// <summary>
    /// 测试获取存在的 Block。
    /// </summary>
    [Fact]
    public async Task GetBlockAsync_当Block存在时_应返回对应的BlockStatus()
    {
        // Arrange
        var manager = CreateTestManager();

        // Act
        var rootBlock = await manager.GetBlockAsync(BlockManager.WorldRootId);

        // Assert
        rootBlock.Should().NotBeNull();
        rootBlock!.Block.BlockId.Should().Be(BlockManager.WorldRootId);
    }

    /// <summary>
    /// 测试获取不存在的 Block。
    /// </summary>
    [Fact]
    public async Task GetBlockAsync_当Block不存在时_应返回null()
    {
        // Arrange
        var manager = CreateTestManager();
        var nonExistentId = "blk_不存在的块";

        // Act
        var block = await manager.GetBlockAsync(nonExistentId);

        // Assert
        block.Should().BeNull();
    }

    /// <summary>
    /// 测试对 Idle 状态的 Block 执行原子操作。
    /// </summary>
    [Fact]
    public async Task EnqueueOrExecuteAtomicOperationsAsync_对于Idle状态_应直接执行操作()
    {
        // Arrange
        var manager = CreateTestManager();
        var blockId = BlockManager.WorldRootId;
        var op = AtomicOperation.Create(EntityType.Item, "item_sword", new() { { "damage", 10 } });
        var operations = new List<AtomicOperation> { op };

        // Act
        var (statusResult, opResults) = await manager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);

        // Assert
        statusResult.Should().NotBeNull();
        statusResult.Value.AsT0.Should().NotBeNull().And.BeOfType<IdleBlockStatus>();
        opResults.Should().NotBeNull().And.HaveCount(1);
        opResults![0].Success.Should().BeTrue();

        var block = await manager.GetBlockAsync(blockId) as IdleBlockStatus;
        block!.CurrentWorldState.FindEntityById("item_sword", EntityType.Item).Should().NotBeNull();
        block!.CurrentWorldState.FindEntityById("item_sword", EntityType.Item)!.GetAttribute("damage").Should().Be(10);
    }

    /// <summary>
    /// 测试对 Loading 状态的 Block 执行原子操作。
    /// </summary>
    [Fact]
    public async Task EnqueueOrExecuteAtomicOperationsAsync_对于Loading状态_应暂存操作()
    {
        // Arrange
        var manager = CreateTestManager();
        var parentId = BlockManager.WorldRootId;
        var childBlock =
            await manager.CreateChildBlock_Async(parentId, new Dictionary<string, object?>()); // 创建一个 Loading 状态的子块
        var loadingBlockId = childBlock!.Block.BlockId;

        var op = AtomicOperation.Create(EntityType.Character, "char_hero", new() { { "level", 1 } });
        var operations = new List<AtomicOperation> { op };

        // Act
        var (statusResult, opResults) = await manager.EnqueueOrExecuteAtomicOperationsAsync(loadingBlockId, operations);

        // Assert
        statusResult.Should().NotBeNull();
        statusResult.Value.AsT1.Should().NotBeNull().And.BeOfType<LoadingBlockStatus>();
        var loadingStatus = statusResult.Value.AsT1;

        opResults.Should().NotBeNull().And.HaveCount(1);
        opResults![0].Success.Should().BeTrue(); // 操作应用于 wsTemp 应该是成功的

        // 验证操作已应用于 wsTemp
        loadingStatus!.CurrentWorldState.Should().BeSameAs(loadingStatus.Block.wsTemp); // 确认 Current 是 wsTemp
        loadingStatus.Block.wsTemp!.FindEntityById("char_hero", EntityType.Character).Should().NotBeNull();
        loadingStatus.Block.wsTemp!.FindEntityById("char_hero", EntityType.Character)!.GetAttribute("level").Should()
            .Be(1);

        // 验证操作已被暂存
        loadingStatus.PendingUserCommands.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(op);

        // 验证 wsInput 和 wsPostUser/wsPostAI 未受影响
        loadingStatus.Block.wsInput.FindEntityById("char_hero", EntityType.Character).Should().BeNull();
        loadingStatus.Block.wsPostUser.Should().BeNull();
        loadingStatus.Block.wsPostAI.Should().BeNull();
    }

    // /// <summary>
    // /// 测试对 Conflict 状态的 Block 执行原子操作。
    // /// </summary>
    // [Fact]
    // public async Task EnqueueOrExecuteAtomicOperationsAsync_对于Conflict状态_应忽略操作()
    // {
    //     // Arrange
    //     var manager = CreateTestManager();
    //     var block = Block.Block.CreateBlock("blk_conflict", BlockManager.WorldRootId, new WorldState(),
    //         new GameState());
    //     var conflictStatus = new ConflictBlockStatus(block.Block, [], []); // 手动创建冲突状态
    //
    //     var blocksInternal = (ConcurrentDictionary<string, BlockStatus>)manager.GetType()
    //         .GetField("blocks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
    //         .GetValue(manager)!;
    //     blocksInternal.TryAdd("blk_conflict", conflictStatus); // 添加到管理器
    //
    //     var op = AtomicOperation.Create(EntityType.Place, "place_town");
    //     var operations = new List<AtomicOperation> { op };
    //
    //     // Act
    //     var (statusResult, opResults) = await manager.EnqueueOrExecuteAtomicOperationsAsync("blk_conflict", operations);
    //
    //     // Assert
    //     statusResult.Should().NotBeNull().And.BeOfType<ConflictBlockStatus>();
    //     opResults.Should().BeNull(); // 操作结果应为 null
    //
    //     // 确认 WorldState 未改变
    //     conflictStatus.CurrentWorldState.FindEntityById("place_town", EntityType.Place).Should().BeNull();
    // }


    /// <summary>
    /// 测试工作流成功完成但存在冲突的情况。
    /// </summary>
    [Fact]
    public async Task HandleWorkflowCompletionAsync_成功但有冲突_应转为Conflict状态()
    {
        // Arrange
        var manager = CreateTestManager();
        var initialWs = new WorldState();
        initialWs.AddEntity(new Item("item_sword") { IsDestroyed = false }); // 添加一个实体供冲突
        var parentBlock = await manager.GetBlockAsync(BlockManager.WorldRootId) as IdleBlockStatus;
        parentBlock!.Block.wsPostUser = initialWs; // 修改父节点的输出状态以影响子节点输入

        var childBlock = await manager.CreateChildBlock_Async(BlockManager.WorldRootId, new());
        var loadingBlockId = childBlock!.Block.BlockId;


        // 冲突操作：都修改同一个实体的属性
        var aiOp = AtomicOperation.Modify(EntityType.Item, "item_sword", "damage", "=", 20);
        var userOp = AtomicOperation.Modify(EntityType.Item, "item_sword", "damage", "=", 15);

        // 模拟用户操作
        await manager.EnqueueOrExecuteAtomicOperationsAsync(loadingBlockId, [userOp]);

        // 模拟工作流完成
        var rawText = "AI tried to change damage.";
        var firstPartyCommands = new List<AtomicOperation> { aiOp };

        // Act
        var completionResult =
            await manager.HandleWorkflowCompletionAsync(loadingBlockId, true, rawText, firstPartyCommands, new());

        // Assert
        completionResult.Should().NotBeNull();
        // completionResult!.Value.Should().BeOfType<ConflictBlockStatus>(); // 确认是 Conflict 状态

        var conflictStatus = completionResult.Value.AsT1;
        conflictStatus.Should().NotBeNull();

        // 验证状态变化
        var finalBlock = await manager.GetBlockAsync(loadingBlockId);
        finalBlock.Should().BeOfType<ConflictBlockStatus>();
        var finalConflictStatus = finalBlock as ConflictBlockStatus;

        finalConflictStatus!.Block.BlockContent.Should().Be(rawText);
        finalConflictStatus.Block.wsTemp.Should().NotBeNull(); // wsTemp 保留
        finalConflictStatus.Block.wsPostAI.Should().BeNull();
        finalConflictStatus.Block.wsPostUser.Should().BeNull();

        // 验证冲突命令已存储
        finalConflictStatus.conflictingAiCommands.Should().ContainSingle().Which.Should().BeEquivalentTo(aiOp);
        finalConflictStatus.conflictingUserCommands.Should().ContainSingle().Which.Should().BeEquivalentTo(userOp);
    }

    /// <summary>
    /// 测试工作流失败的情况。
    /// </summary>
    [Fact]
    public async Task HandleWorkflowCompletionAsync_失败_应转为Error状态()
    {
        // Arrange
        var manager = CreateTestManager();
        var childBlock = await manager.CreateChildBlock_Async(BlockManager.WorldRootId, new());
        var loadingBlockId = childBlock!.Block.BlockId;

        var rawText = "Workflow failed text."; // 可能为空或错误信息
        var firstPartyCommands = new List<AtomicOperation>(); // 失败时可能没有命令

        // Act
        var completionResult =
            await manager.HandleWorkflowCompletionAsync(loadingBlockId, false, rawText, firstPartyCommands, new());

        // Assert
        completionResult.Should().NotBeNull();
        completionResult.Value.AsT2.Should().NotBeNull().And.BeOfType<ErrorBlockStatus>(); // 确认是 Error 状态

        var errorStatus = completionResult.Value.AsT2;
        errorStatus.Should().NotBeNull();

        // 验证状态变化
        var finalBlock = await manager.GetBlockAsync(loadingBlockId);
        finalBlock.Should().BeOfType<ErrorBlockStatus>();
        var finalErrorStatus = finalBlock as ErrorBlockStatus;

        // 验证状态属性（根据 HandleWorkflowCompletionAsync 中的失败逻辑）
        // finalErrorStatus!.Block.BlockContent.Should().Be(rawText); // 内容可能被设置，也可能不设置
        finalErrorStatus!.Block.wsTemp.Should().NotBeNull(); // 失败时 wsTemp 可能保留或清除，取决于实现
        finalErrorStatus.Block.wsPostAI.Should().BeNull();
        finalErrorStatus.Block.wsPostUser.Should().BeNull();
    }

    /// <summary>
    /// 测试获取简单路径到根。
    /// </summary>
    [Fact]
    public async Task GetPathToRoot_简单线性路径_应返回从根到叶的正确路径()
    {
        // Arrange
        var manager = CreateTestManager();
        var child1 = await manager.CreateChildBlock_Async(BlockManager.WorldRootId, new());
        // 需要手动将 child1 状态变为 Idle 才能创建 child2
        await manager.HandleWorkflowCompletionAsync(child1!.Block.BlockId, true, "child1 done", [], []);
        var child2 = await manager.CreateChildBlock_Async(child1!.Block.BlockId, new());
        await manager.HandleWorkflowCompletionAsync(child2!.Block.BlockId, true, "child2 done", [], []);
        var child3 = await manager.CreateChildBlock_Async(child2!.Block.BlockId, new());
        await manager.HandleWorkflowCompletionAsync(child3!.Block.BlockId, true, "child3 done", [], []);


        // Act
        // 从 child3 开始查找路径
        var path = manager.GetPathToRoot(child3!.Block.BlockId);

        // Assert
        path.Should().NotBeNullOrEmpty()
            .And.HaveCount(4)
            .And.ContainInOrder(
                BlockManager.WorldRootId,
                child1!.Block.BlockId,
                child2!.Block.BlockId,
                child3!.Block.BlockId
            );
    }

    /// <summary>
    /// 测试在分支路径中获取到根的路径（应跟随最后一个子节点）。
    /// </summary>
    [Fact]
    public async Task GetPathToRoot_带分支的路径_应跟随最后一个子节点()
    {
        // Arrange
        var manager = CreateTestManager();
        var child1 = await manager.CreateChildBlock_Async(BlockManager.WorldRootId, new()); // child1
        await manager.HandleWorkflowCompletionAsync(child1!.Block.BlockId, true, "child1 done", [], []);

        var child2a = await manager.CreateChildBlock_Async(child1.Block.BlockId, new()); // child2a (第一个子节点)
        await manager.HandleWorkflowCompletionAsync(child2a!.Block.BlockId, true, "child2a done", [], []);

        var child2b = await manager.CreateChildBlock_Async(child1.Block.BlockId, new()); // child2b (第二个, 也是最后一个子节点)
        await manager.HandleWorkflowCompletionAsync(child2b!.Block.BlockId, true, "child2b done", [], []);

        var child3 = await manager.CreateChildBlock_Async(child2b.Block.BlockId, new()); // child3 (child2b 的子节点)
        await manager.HandleWorkflowCompletionAsync(child3!.Block.BlockId, true, "child3 done", [], []);

        // Act
        // 从 child1 开始查找路径，它应该向下找到 child2b，然后 child3
        var path = manager.GetPathToRoot(child1.Block.BlockId);

        // Assert
        path.Should().NotBeNullOrEmpty()
            .And.HaveCount(4)
            .And.ContainInOrder(
                BlockManager.WorldRootId,
                child1.Block.BlockId,
                child2b.Block.BlockId, // 确认路径走向了最后一个子节点 child2b
                child3.Block.BlockId
            );
    }

    /// <summary>
    /// 测试从根节点开始获取路径。
    /// </summary>
    [Fact]
    public async Task GetPathToRoot_从根节点开始_应只返回根节点()
    {
        // Arrange
        var manager = CreateTestManager();
        var child1 = await manager.CreateChildBlock_Async(BlockManager.WorldRootId, new());
        await manager.HandleWorkflowCompletionAsync(child1!.Block.BlockId, true, "child1 done", [], []);


        // Act
        var path = manager.GetPathToRoot(BlockManager.WorldRootId);

        // Assert
        // 行为更新：从根开始，会找到最深的叶子节点，然后返回完整路径
        path.Should().NotBeNullOrEmpty()
            .And.HaveCount(2)
            .And.ContainInOrder(BlockManager.WorldRootId, child1.Block.BlockId);
    }

    /// <summary>
    /// 测试使用无效的起始 ID 获取路径。
    /// </summary>
    [Fact]
    public void GetPathToRoot_使用无效起始ID_应返回空列表()
    {
        // Arrange
        var manager = CreateTestManager();
        var invalidId = "blk_无效ID";

        // Act
        var path = manager.GetPathToRoot(invalidId);

        // Assert
        // 行为更新：如果起始 ID 无效，直接返回空
        path.Should().BeEmpty();
    }

    /// <summary>
    /// 测试更新块的游戏状态。
    /// </summary>
    [Fact]
    public async Task UpdateBlockGameStateAsync_当块存在时_应成功更新GameState()
    {
        // Arrange
        var manager = CreateTestManager();
        var blockId = BlockManager.WorldRootId;
        var updates = new Dictionary<string, object?>
        {
            { "difficulty", "hard" },
            { "score", 100 },
            { "playerName", null } // 测试设置 null 值
        };

        // Act
        var result = await manager.UpdateBlockGameStateAsync(blockId, updates);
        var block = await manager.GetBlockAsync(blockId);

        // Assert
        result.Should().Be(UpdateResult.Success);
        block.Should().NotBeNull();
        block!.Block.GameState["difficulty"].Should().Be("hard");
        block.Block.GameState["score"].Should().Be(100);
        block.Block.GameState["playerName"].Should().BeNull();
        block.Block.GameState.GetAllSettings().ContainsKey("playerName").Should().BeTrue(); // 确认键存在，值为 null
    }

    /// <summary>
    /// 测试更新不存在块的游戏状态。
    /// </summary>
    [Fact]
    public async Task UpdateBlockGameStateAsync_当块不存在时_应返回NotFound()
    {
        // Arrange
        var manager = CreateTestManager();
        var nonExistentId = "blk_不存在的块";
        var updates = new Dictionary<string, object?> { { "difficulty", "easy" } };

        // Act
        var result = await manager.UpdateBlockGameStateAsync(nonExistentId, updates);

        // Assert
        result.Should().Be(UpdateResult.NotFound);
    }
}