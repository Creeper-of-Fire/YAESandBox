// --- START OF FILE BlockManagerModificationTests.cs ---

using Xunit;
using FluentAssertions;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Core.Action;
using System.Collections.Generic;
using System.Threading.Tasks;
using OneOf; // For OneOf types

namespace YAESandBox.Tests.Core;

/// <summary>
/// BlockManager 的修改相关操作的单元测试，特别是状态转换和原子操作处理。
/// </summary>
public class BlockManagerModificationTests
{
    // --- 辅助方法 ---

    /// <summary>
    /// 创建一个包含基本 WorldState 和 GameState 的测试用 Block。
    /// </summary>
    private static Block CreateTestBlock(string blockId = "testBlock", string? parentId = BlockManager.WorldRootId,
        WorldState? wsInput = null, GameState? gs = null)
    {
        var inputWs = wsInput ?? new WorldState();
        var gameState = gs ?? new GameState();
        // 使用内部构造函数或 CreateBlockFromSave（如果需要预设状态）
        // 这里我们假设需要一个基础的 Idle 状态 Block 来进行后续修改
        var initialIdleStatus = YAESandBox.Core.Block.Block.CreateBlockFromSave(
            blockId,
            parentId,
            new List<string>(), // children
            "Initial Content",
            new Dictionary<string, string> { { "InitialMeta", "Value" } }, // metadata
            new Dictionary<string, object?>(), // triggered params
            gameState,
            inputWs, // wsInput
            null, // wsPostAI initially null for manual setup
            inputWs.Clone() // wsPostUser starts as clone of wsInput for Idle
        );
        return initialIdleStatus.Block; // 返回底层的 Block 对象
    }

    /// <summary>
    /// 创建一个 Idle 状态的 BlockStatus。
    /// </summary>
    private static IdleBlockStatus CreateIdleStatus(Block block)
    {
        // 确保 wsPostUser 存在 (对于从 CreateTestBlock 创建的应该已经存在)
        block.wsPostUser ??= block.wsInput.Clone();
        block.wsPostAI = null; // Idle 状态通常没有 wsPostAI (除非刚完成)
        block.wsTemp = null;
        return new IdleBlockStatus(block);
    }

    /// <summary>
    /// 创建一个 Loading 状态的 BlockStatus。
    /// </summary>
    private static LoadingBlockStatus CreateLoadingStatus(Block block,
        List<AtomicOperation>? pendingUserCommands = null)
    {
        block.wsTemp ??= block.wsInput.Clone(); // Loading 需要 wsTemp
        block.wsPostAI = null;
        block.wsPostUser = null;
        var status = new LoadingBlockStatus(block);
        if (pendingUserCommands != null)
        {
            // 注意：ApplyOperations 会修改 wsTemp，这里仅预设挂起列表
            status.PendingUserCommands.AddRange(pendingUserCommands);
        }

        return status;
    }

    /// <summary>
    /// 创建一个 Conflict 状态的 BlockStatus。
    /// </summary>
    private static ConflictBlockStatus CreateConflictStatus(Block block)
    {
        block.wsTemp ??= block.wsInput.Clone(); // Conflict 也基于 wsTemp
        block.wsPostAI = null;
        block.wsPostUser = null;
        // 提供空的冲突列表，因为具体冲突内容在此测试中不重要
        return new ConflictBlockStatus(block,
            new List<AtomicOperation>(), // conflictAiCommands
            new List<AtomicOperation>(), // conflictUserCommands
            new List<AtomicOperation>(), // aiCommands
            new List<AtomicOperation>()); // userCommands
    }

    /// <summary>
    /// 创建一个 Error 状态的 BlockStatus。
    /// </summary>
    private static ErrorBlockStatus CreateErrorStatus(Block block)
    {
        block.wsPostAI = null;
        block.wsPostUser = null;
        block.wsTemp = null;
        return new ErrorBlockStatus(block);
    }


    // --- ApplyResolvedCommandsAsync Tests ---

    [Fact]
    [Trait("测试方法", "ApplyResolvedCommandsAsync")]
    public async Task ApplyResolvedCommandsAsync_当块不存在时_应返回Null()
    {
        // Arrange (准备)
        var manager = new BlockManager();
        var blockId = "nonExistentBlock";
        var resolvedCommands = new List<AtomicOperation>
            { AtomicOperation.Create(EntityType.Item, "item1", new() { { "name", "测试物品" } }) };

        // Act (执行)
        var (blockStatus, results) = await manager.ApplyResolvedCommandsAsync(blockId, resolvedCommands);

        // Assert (断言)
        blockStatus.Should().BeNull("因为块不存在");
        results.Should().BeNull("因为块不存在");
    }

    [Theory]
    [Trait("测试方法", "ApplyResolvedCommandsAsync")]
    [InlineData(BlockStatusCode.Idle)] // 测试 Idle 状态
    [InlineData(BlockStatusCode.Loading)] // 测试 Loading 状态
    [InlineData(BlockStatusCode.Error)] // 测试 Error 状态
    public async Task ApplyResolvedCommandsAsync_当块状态不是Conflict时_应返回Null(BlockStatusCode initialStatusCode)
    {
        // Arrange (准备)
        var manager = new BlockManager();
        var blockId = "testBlock";
        var block = CreateTestBlock(blockId);
        BlockStatus initialStatus = initialStatusCode switch
        {
            BlockStatusCode.Idle => CreateIdleStatus(block),
            BlockStatusCode.Loading => CreateLoadingStatus(block),
            BlockStatusCode.Error => CreateErrorStatus(block),
            _ => throw new ArgumentOutOfRangeException(nameof(initialStatusCode)) // Conflict 状态不在此测试
        };
        manager.TrySetBlock(initialStatus); // 使用 internal 方法设置初始状态

        var resolvedCommands = new List<AtomicOperation>
            { AtomicOperation.Create(EntityType.Item, "item1", new() { { "name", "测试物品" } }) };

        // Act (执行)
        var (blockStatus, results) = await manager.ApplyResolvedCommandsAsync(blockId, resolvedCommands);

        // Assert (断言)
        blockStatus.Should().BeNull($"因为块的状态是 {initialStatusCode} 而不是 Conflict");
        results.Should().BeNull($"因为块的状态是 {initialStatusCode} 而不是 Conflict");
    }

    [Fact]
    [Trait("测试方法", "ApplyResolvedCommandsAsync")]
    public async Task ApplyResolvedCommandsAsync_当块为Conflict且指令有效时_应转换为Idle状态并返回成功结果()
    {
        // Arrange (准备)
        var manager = new BlockManager();
        var blockId = "conflictBlock";
        var block = CreateTestBlock(blockId);
        var conflictStatus = CreateConflictStatus(block);
        manager.TrySetBlock(conflictStatus); // 设置为冲突状态

        var resolvedCommands = new List<AtomicOperation>
        {
            AtomicOperation.Create(EntityType.Item, "item1", new() { { "name", "新物品" } }),
            AtomicOperation.Modify(EntityType.Item, "item1", "description", "=", "物品描述") // 修改刚创建的
        };

        // Act (执行)
        var (returnedStatusUnion, results) = await manager.ApplyResolvedCommandsAsync(blockId, resolvedCommands);

        // Assert (断言)
        // 1. 检查返回的状态和结果
        returnedStatusUnion.Should().NotBeNull("因为操作应该成功转换状态");
        // returnedStatusUnion!.Value.Should().BeOfType<IdleBlockStatus>("因为冲突解决成功后应进入 Idle 状态");
        results.Should().NotBeNull("应该返回操作结果");
        results!.Should().HaveCount(resolvedCommands.Count, "每个指令都应该有结果");
        results.Should().OnlyContain(r => r.Success, "所有指令都应该成功执行");

        // 2. 检查 BlockManager 中的最终状态
        var finalStatus = await manager.GetBlockAsync(blockId);
        finalStatus.Should().NotBeNull();
        finalStatus.Should().BeOfType<IdleBlockStatus>("管理器中的块状态应更新为 Idle");

        // 3. 检查底层 Block 的状态 (可选，但有助于确认逻辑)
        var finalBlock = ((IdleBlockStatus)finalStatus!).Block;
        finalBlock.wsPostUser.Should().NotBeNull("Idle 状态必须有 wsPostUser");
        finalBlock.wsPostUser!.FindEntityById("item1", EntityType.Item).Should().NotBeNull("创建的物品应存在于 wsPostUser");
        finalBlock.wsPostUser!.FindEntityById("item1", EntityType.Item)!.GetAttribute("description").Should()
            .Be("物品描述", "修改应该生效");
        finalBlock.wsTemp.Should().BeNull("冲突解决后 wsTemp 应被清理");
        finalBlock.wsPostAI.Should().NotBeNull("冲突解决成功会先生成 wsPostAI"); // 注意：wsPostAI 在成功解决后会基于 wsInput + resolved 生成
        finalBlock.wsPostUser.Items.Should()
            .BeEquivalentTo(finalBlock.wsPostAI.Items, "wsPostUser 应该等于 wsPostAI"); // 在 Idle 状态下，两者应该同步
    }

    [Fact]
    [Trait("测试方法", "ApplyResolvedCommandsAsync")]
    public async Task ApplyResolvedCommandsAsync_当解决指令执行失败时_应转换为Error状态并返回失败结果()
    {
        // Arrange (准备)
        var manager = new BlockManager();
        var blockId = "conflictBlockFail";
        var block = CreateTestBlock(blockId); // wsInput 是空的
        var conflictStatus = CreateConflictStatus(block);
        manager.TrySetBlock(conflictStatus);

        // 尝试修改一个不存在的实体，这将导致失败
        var resolvedCommands = new List<AtomicOperation>
        {
            AtomicOperation.Modify(EntityType.Item, "nonExistentItem", "name", "=", "失败的名字")
        };

        // Act (执行)
        var (returnedStatusUnion, results) = await manager.ApplyResolvedCommandsAsync(blockId, resolvedCommands);

        // Assert (断言)
        // 1. 检查返回的状态和结果
        returnedStatusUnion.Should().NotBeNull("因为操作应该导致状态转换");
        // returnedStatusUnion!.Value.Should().BeOfType<ErrorBlockStatus>("因为解决指令执行失败，应进入 Error 状态");
        results.Should().NotBeNull("应该返回操作结果");
        results!.Should().HaveCount(resolvedCommands.Count);
        results.Should()
            .ContainSingle(r => !r.Success && r.ErrorMessage != null && r.OriginalOperation == resolvedCommands[0],
                "修改不存在实体的指令应失败");

        // 2. 检查 BlockManager 中的最终状态
        var finalStatus = await manager.GetBlockAsync(blockId);
        finalStatus.Should().NotBeNull();
        finalStatus.Should().BeOfType<ErrorBlockStatus>("管理器中的块状态应更新为 Error");

        // 3. 检查底层 Block 的状态
        var finalBlock = ((ErrorBlockStatus)finalStatus!).Block;
        finalBlock.wsPostUser.Should().BeNull("Error 状态不应有 wsPostUser");
        finalBlock.wsPostAI.Should().BeNull("指令失败时不应保留 wsPostAI");
        finalBlock.wsTemp.Should().BeNull("wsTemp 应被清理");
        // 可以检查 Metadata 是否记录了错误信息 (如果 BlockStatus.cs 中实现了)
        // finalBlock.Metadata.Should().ContainKey("Error");
    }


    // --- EnqueueOrExecuteAtomicOperationsAsync Tests ---

    [Fact]
    [Trait("测试方法", "EnqueueOrExecuteAtomicOperationsAsync")]
    public async Task EnqueueOrExecute_当块为Idle且操作有效时_应执行操作并返回Idle和成功结果()
    {
        // Arrange (准备)
        var manager = new BlockManager();
        var blockId = "idleBlock";
        var block = CreateTestBlock(blockId);
        var idleStatus = CreateIdleStatus(block);
        manager.TrySetBlock(idleStatus);

        var operations = new List<AtomicOperation>
        {
            AtomicOperation.Create(EntityType.Character, "char1", new() { { "name", "英雄" } })
        };

        // Act (执行)
        var (returnedStatusUnion, results) = await manager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);

        // Assert (断言)
        // 1. 返回值检查
        returnedStatusUnion.Should().NotBeNull();
        // returnedStatusUnion!.Value.Should().BeOfType<IdleBlockStatus>("状态应保持 Idle");
        results.Should().NotBeNull();
        results!.Should().ContainSingle(r => r.Success && r.OriginalOperation == operations[0]);

        // 2. 状态检查
        var finalStatus = await manager.GetBlockAsync(blockId);
        finalStatus.Should().BeOfType<IdleBlockStatus>();

        // 3. WorldState 检查 (wsPostUser 应被修改)
        var finalBlock = ((IdleBlockStatus)finalStatus!).Block;
        finalBlock.wsPostUser.Should().NotBeNull();
        finalBlock.wsPostUser!.FindEntityById("char1", EntityType.Character).Should().NotBeNull("角色应已创建");
        finalBlock.wsPostUser!.FindEntityById("char1", EntityType.Character)!.GetAttribute("name").Should().Be("英雄");
        finalBlock.wsTemp.Should().BeNull("Idle 状态不应有 wsTemp");
        finalBlock.wsPostAI.Should().BeNull("Idle 状态直接修改 wsPostUser");
    }

    [Fact]
    [Trait("测试方法", "EnqueueOrExecuteAtomicOperationsAsync")]
    public async Task EnqueueOrExecute_当块为Loading且操作有效时_应排队操作并返回Loading和成功结果()
    {
        // Arrange (准备)
        var manager = new BlockManager();
        var blockId = "loadingBlock";
        var block = CreateTestBlock(blockId);
        var loadingStatus = CreateLoadingStatus(block); // 初始 pending 为空
        manager.TrySetBlock(loadingStatus);

        var operations = new List<AtomicOperation>
        {
            AtomicOperation.Create(EntityType.Place, "place1", new() { { "name", "城堡" } })
        };

        // Act (执行)
        var (returnedStatusUnion, results) = await manager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);

        // Assert (断言)
        // 1. 返回值检查
        returnedStatusUnion.Should().NotBeNull();
        // var returnedLoadingStatus = returnedStatusUnion!.Value.Should().BeOfType<LoadingBlockStatus>("状态应保持 Loading").Subject;
        results.Should().NotBeNull();
        results!.Should().ContainSingle(r => r.Success && r.OriginalOperation == operations[0]);

        // 2. 状态检查
        var finalStatus = await manager.GetBlockAsync(blockId);
        var finalLoadingStatus = finalStatus.Should().BeOfType<LoadingBlockStatus>().Subject;

        // 3. WorldState 检查 (wsTemp 应被修改)
        finalLoadingStatus.Block.wsTemp.Should().NotBeNull();
        finalLoadingStatus.Block.wsTemp!.FindEntityById("place1", EntityType.Place).Should().NotBeNull("地点应存在于 wsTemp");

        // 4. PendingCommands 检查 (成功操作应被加入)
        finalLoadingStatus.PendingUserCommands.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(operations[0], "成功的操作应被添加到 PendingUserCommands");

        // 5. 其他状态检查
        finalLoadingStatus.Block.wsPostUser.Should().BeNull();
        finalLoadingStatus.Block.wsPostAI.Should().BeNull();
    }

    [Fact]
    [Trait("测试方法", "EnqueueOrExecuteAtomicOperationsAsync")]
    public async Task EnqueueOrExecute_当块为Loading且部分操作失败时_应返回Loading和混合结果且仅排队成功操作()
    {
        // Arrange (准备)
        var manager = new BlockManager();
        var blockId = "loadingBlockPartialFail";
        var block = CreateTestBlock(blockId); // wsInput 是空的
        var loadingStatus = CreateLoadingStatus(block);
        manager.TrySetBlock(loadingStatus);

        var operations = new List<AtomicOperation>
        {
            AtomicOperation.Create(EntityType.Item, "itemSuccess", new() { { "name", "成功物品" } }), // 会成功
            AtomicOperation.Modify(EntityType.Item, "itemFail", "name", "=", "失败修改") // 会失败，因为 itemFail 不存在
        };

        // Act (执行)
        var (returnedStatusUnion, results) = await manager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);

        // Assert (断言)
        // 1. 返回值检查
        returnedStatusUnion.Should().NotBeNull();
        // var returnedLoadingStatus = returnedStatusUnion!.Value.Should().BeOfType<LoadingBlockStatus>().Subject;
        results.Should().NotBeNull();
        results!.Should().HaveCount(2);
        results.Should().ContainSingle(r => r.Success && r.OriginalOperation == operations[0]);
        results.Should().ContainSingle(r => !r.Success && r.OriginalOperation == operations[1]);

        // 2. 状态检查
        var finalStatus = await manager.GetBlockAsync(blockId);
        var finalLoadingStatus = finalStatus.Should().BeOfType<LoadingBlockStatus>().Subject;

        // 3. WorldState 检查 (wsTemp 应反映 *所有* 尝试的操作)
        finalLoadingStatus.Block.wsTemp.Should().NotBeNull();
        finalLoadingStatus.Block.wsTemp!.FindEntityById("itemSuccess", EntityType.Item).Should()
            .NotBeNull("成功创建的物品应在 wsTemp");
        finalLoadingStatus.Block.wsTemp!.FindEntityById("itemFail", EntityType.Item).Should().BeNull("失败修改的目标不应存在");

        // 4. PendingCommands 检查 (仅包含成功的操作)
        finalLoadingStatus.PendingUserCommands.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(operations[0], "只有成功的操作应被添加到 PendingUserCommands");
    }

    [Theory]
    [Trait("测试方法", "EnqueueOrExecuteAtomicOperationsAsync")]
    [InlineData(BlockStatusCode.ResolvingConflict)]
    [InlineData(BlockStatusCode.Error)]
    public async Task EnqueueOrExecute_当块为Conflict或Error时_不执行操作并返回原状态和Null结果(BlockStatusCode initialStatusCode)
    {
        // Arrange (准备)
        var manager = new BlockManager();
        var blockId = "blockInFinalState";
        var block = CreateTestBlock(blockId);
        BlockStatus initialStatus = initialStatusCode switch
        {
            BlockStatusCode.ResolvingConflict => CreateConflictStatus(block),
            BlockStatusCode.Error => CreateErrorStatus(block),
            _ => throw new ArgumentOutOfRangeException(nameof(initialStatusCode))
        };
        manager.TrySetBlock(initialStatus);
        var initialWorldStateClone = block.wsInput.Clone(); // 克隆初始状态用于比较

        var operations = new List<AtomicOperation>
        {
            AtomicOperation.Create(EntityType.Item, "item1", new() { { "name", "不应创建的物品" } })
        };

        // Act (执行)
        var (returnedStatusUnion, results) = await manager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);

        // Assert (断言)
        // 1. 返回值检查
        returnedStatusUnion.Should().NotBeNull();
        returnedStatusUnion!.Value
            .ForceResult<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus, BlockStatus,
                BlockStatusCode>((target => target.StatusCode)).Should().Be(initialStatusCode, "状态不应改变");
        // returnedStatusUnion.Value.Should().BeSameAs(initialStatus, "应返回原始状态对象实例"); // 确认是同一个实例
        results.Should().BeNull("不应执行任何操作，因此结果为 null");

        // 2. 状态检查
        var finalStatus = await manager.GetBlockAsync(blockId);
        finalStatus.Should().BeOfType(initialStatus.GetType()); // 确认类型未变

        // 3. WorldState 检查 (不应有任何变化)
        // 注意：Conflict 状态下 CurrentWorldState 是 wsTemp，Error 状态下是 wsPostUser (但通常为 null)
        // 最稳妥是检查 Block 内部状态
        var finalBlock = finalStatus!.Block;
        finalBlock.wsInput.Items.Should().BeEquivalentTo(initialWorldStateClone.Items); // wsInput 不应变
        finalBlock.wsInput.Characters.Should().BeEquivalentTo(initialWorldStateClone.Characters);
        finalBlock.wsInput.Places.Should().BeEquivalentTo(initialWorldStateClone.Places);

        if (finalStatus is ConflictBlockStatus cbs)
        {
            cbs.Block.wsTemp.Should().NotBeNull();
            // 比较 wsTemp 和 wsInput，它们在 Conflict 状态初始化后应该相等，且不应被修改
            cbs.Block.wsTemp!.Items.Should().BeEquivalentTo(initialWorldStateClone.Items);
            cbs.Block.wsTemp!.Characters.Should().BeEquivalentTo(initialWorldStateClone.Characters);
            cbs.Block.wsTemp!.Places.Should().BeEquivalentTo(initialWorldStateClone.Places);
        }
        else if (finalStatus is ErrorBlockStatus ebs)
        {
            ebs.Block.wsTemp.Should().BeNull();
            ebs.Block.wsPostAI.Should().BeNull();
            ebs.Block.wsPostUser.Should().BeNull();
        }
    }

    [Fact]
    [Trait("测试方法", "EnqueueOrExecuteAtomicOperationsAsync")]
    public async Task EnqueueOrExecute_当块不存在时_应返回Null()
    {
        // Arrange (准备)
        var manager = new BlockManager();
        var blockId = "nonExistentBlock";
        var operations = new List<AtomicOperation> { AtomicOperation.Create(EntityType.Item, "item1") };

        // Act (执行)
        var (returnedStatusUnion, results) = await manager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);

        // Assert (断言)
        returnedStatusUnion.Should().BeNull();
        results.Should().BeNull();
    }


    // --- ConflictBlockStatus.FinalizeConflictResolution (通过 ApplyResolvedCommandsAsync 间接测试) ---
    // 上面的 ApplyResolvedCommandsAsync 测试已经覆盖了 FinalizeConflictResolution 的成功和失败路径。
    // 如果需要更细粒度地直接测试 FinalizeConflictResolution，可以这样做：

    [Fact]
    [Trait("测试方法", "ConflictBlockStatus.FinalizeConflictResolution")]
    public void FinalizeConflictResolution_当指令有效时_应返回Idle状态和成功结果()
    {
        // Arrange (准备)
        var block = CreateTestBlock("conflictBlockDirect");
        var conflictStatus = CreateConflictStatus(block);
        var resolvedCommands = new List<AtomicOperation>
        {
            AtomicOperation.Create(EntityType.Item, "itemDirect", new() { { "name", "直接创建" } })
        };
        string dummyRawContent = "Resolved Content"; // 模拟内容

        // Act (执行) - 直接调用 ConflictBlockStatus 的方法
        // 注意： FinalizeConflictResolution 内部会创建 Loading 然后调用 _FinalizeSuccessfulWorkflow
        var (returnedStatusUnion, results) =
            conflictStatus.FinalizeConflictResolution(dummyRawContent, resolvedCommands);

        // Assert (断言)
        // 1. 检查返回的状态和结果
        returnedStatusUnion.Should().NotBeNull();
        var idleStatus = returnedStatusUnion.Value.Should().BeOfType<IdleBlockStatus>().Subject;
        results.Should().NotBeNull();
        results!.Should().ContainSingle(r => r.Success);

        // 2. 检查底层 Block 的状态 (这是直接测试的关键)
        var finalBlock = idleStatus.Block; // 获取返回的 Idle 状态关联的 Block
        finalBlock.BlockContent.Should().Be(dummyRawContent);
        finalBlock.wsPostUser.Should().NotBeNull();
        finalBlock.wsPostUser!.FindEntityById("itemDirect", EntityType.Item).Should().NotBeNull();
        finalBlock.wsPostAI.Should().NotBeNull(); // wsPostAI 应该也被正确创建
        finalBlock.wsPostUser.Items.Should().BeEquivalentTo(finalBlock.wsPostAI.Items);
        finalBlock.wsTemp.Should().BeNull();
    }

    [Fact]
    [Trait("测试方法", "ConflictBlockStatus.FinalizeConflictResolution")]
    public void FinalizeConflictResolution_当指令执行失败时_应返回Error状态和失败结果()
    {
        // Arrange (准备)
        var block = CreateTestBlock("conflictBlockDirectFail"); // wsInput 为空
        var conflictStatus = CreateConflictStatus(block);
        var resolvedCommands = new List<AtomicOperation>
        {
            AtomicOperation.Modify(EntityType.Item, "nonExistentItem", "name", "=", "失败修改")
        };
        string dummyRawContent = "Failed Resolution Content";

        // Act (执行)
        var (returnedStatusUnion, results) =
            conflictStatus.FinalizeConflictResolution(dummyRawContent, resolvedCommands);

        // Assert (断言)
        // 1. 检查返回的状态和结果
        returnedStatusUnion.Should().NotBeNull();
        var errorStatus = returnedStatusUnion.Value.Should().BeOfType<ErrorBlockStatus>().Subject;
        results.Should().NotBeNull();
        results!.Should().ContainSingle(r => !r.Success);

        // 2. 检查底层 Block 的状态
        var finalBlock = errorStatus.Block;
        finalBlock.BlockContent.Should().Be(dummyRawContent); // 内容仍然设置
        finalBlock.wsPostUser.Should().BeNull();
        finalBlock.wsPostAI.Should().BeNull();
        finalBlock.wsTemp.Should().BeNull();
        // finalBlock.Metadata.Should().ContainKey("Error"); // 检查错误是否记录
    }
}

// --- END OF FILE BlockManagerModificationTests.cs ---