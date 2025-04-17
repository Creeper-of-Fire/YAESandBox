using Xunit;
using FluentAssertions;
using NSubstitute;
using YAESandBox.API.Services;
using YAESandBox.API.DTOs;
using YAESandBox.Core.Action;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OneOf;
using YAESandBox.API.Controllers; // 引入 OneOf

namespace YAESandBox.Tests.API.Services;

public class BlockWritServiceTests
{
    private readonly INotifierService _notifierServiceMock;
    private readonly IBlockManager _blockManagerMock;
    private readonly BlockWritService _sut;

    public BlockWritServiceTests()
    {
        _notifierServiceMock = Substitute.For<INotifierService>();
        _blockManagerMock = Substitute.For<IBlockManager>();
        _sut = new BlockWritService(_notifierServiceMock, _blockManagerMock);
    }

    private static List<AtomicOperation> CreateSampleOperations() =>
    [
        AtomicOperation.Create(EntityType.Item, "item-op-1", new() { { "name", "操作物品" } }),
        AtomicOperation.Modify(EntityType.Character, "char-op-1", "hp", Operator.Sub, 10)
    ];

    private static List<OperationResult> CreateSampleResults(List<AtomicOperation> ops, bool success = true) =>
        ops.Select(op => success ? OperationResult.Ok(op) : OperationResult.Fail(op, "模拟失败")).ToList();

    // --- 测试 EnqueueOrExecuteAtomicOperationsAsync ---

    [Fact]
    public async Task EnqueueOrExecuteAtomicOperationsAsync_当Block为Idle时_应执行并通知()
    {
        // Arrange
        var blockId = "idle-block";
        var operations = CreateSampleOperations();
        var idleStatus = Block.CreateBlock(blockId, null, new WorldState(), new GameState()).ForceIdleState();
        var results = CreateSampleResults(operations);
        // 使用 OneOf 类型来构造返回值
        OneOf<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus> statusOneOf = idleStatus;
        _blockManagerMock.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations)
                         .Returns(Task.FromResult<(OneOf<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus>? blockStatus, List<OperationResult>? results)>((statusOneOf, results)));


        // Act
        var result = await _sut.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);

        // Assert
        result.Should().Be(AtomicExecutionResult.Executed);
        // 验证 BlockManager 被调用
        await _blockManagerMock.Received(1).EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);
        // 验证 Notifier 被调用
        await _notifierServiceMock.Received(1).NotifyBlockStatusUpdateAsync(blockId, BlockStatusCode.Idle);
        await _notifierServiceMock.Received(1).NotifyStateUpdateAsync(blockId, Arg.Is<IEnumerable<string>>(ids => ids.Count() == 2)); // 验证实体 ID 列表
    }

    [Fact]
    public async Task EnqueueOrExecuteAtomicOperationsAsync_当Block为Loading时_应排队并通知()
    {
        // Arrange
        var blockId = "loading-block";
        var operations = CreateSampleOperations();
        var loadingStatus = Block.CreateBlock(blockId, null, new WorldState(), new GameState()); // Loading 状态
        var results = CreateSampleResults(operations);
        OneOf<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus> statusOneOf = loadingStatus;
         _blockManagerMock.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations)
                          .Returns(Task.FromResult<(OneOf<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus>? blockStatus, List<OperationResult>? results)>((statusOneOf, results)));

        // Act
        var result = await _sut.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);

        // Assert
        result.Should().Be(AtomicExecutionResult.ExecutedAndQueued);
        await _blockManagerMock.Received(1).EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);
        await _notifierServiceMock.Received(1).NotifyBlockStatusUpdateAsync(blockId, BlockStatusCode.Loading);
        await _notifierServiceMock.Received(1).NotifyStateUpdateAsync(blockId, Arg.Any<IEnumerable<string>>());
    }

     [Fact]
    public async Task EnqueueOrExecuteAtomicOperationsAsync_当Block为Conflict时_应返回Conflict且不通知State()
    {
        // Arrange
        var blockId = "conflict-block";
        var operations = CreateSampleOperations();
        var conflictStatus = new ConflictBlockStatus(Block.CreateBlock(blockId, null, new WorldState(), new GameState()).Block, [], [], [], []); // Conflict 状态
        OneOf<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus> statusOneOf = conflictStatus;
         _blockManagerMock.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations)
                          .Returns(Task.FromResult<(OneOf<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus>? blockStatus, List<OperationResult>? results)>((statusOneOf, null))); // Conflict 时 results 为 null

        // Act
        var result = await _sut.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);

        // Assert
        result.Should().Be(AtomicExecutionResult.ConflictState);
        await _blockManagerMock.Received(1).EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);
        // 不应通知状态更新，但 BlockStatus 可能仍会更新（取决于实现，这里假设不更新或不关心）
        await _notifierServiceMock.DidNotReceive().NotifyStateUpdateAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>());
         // 可能需要通知状态变为 Conflict，如果这是预期行为
         // await _notifierServiceMock.Received(1).NotifyBlockStatusUpdateAsync(blockId, BlockStatusCode.ResolvingConflict);
    }

     [Fact]
    public async Task EnqueueOrExecuteAtomicOperationsAsync_当Block为Error时_应返回Error且不通知State()
    {
        // Arrange
        var blockId = "error-block";
        var operations = CreateSampleOperations();
        var errorStatus = new ErrorBlockStatus(Block.CreateBlock(blockId, null, new WorldState(), new GameState()).Block); // Error 状态
        OneOf<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus> statusOneOf = errorStatus;
        _blockManagerMock.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations)
                         .Returns(Task.FromResult<(OneOf<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus>? blockStatus, List<OperationResult>? results)>((statusOneOf, null))); // Error 时 results 为 null


        // Act
        var result = await _sut.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);

        // Assert
        result.Should().Be(AtomicExecutionResult.Error);
        await _blockManagerMock.Received(1).EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);
        await _notifierServiceMock.DidNotReceive().NotifyStateUpdateAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>());
        // await _notifierServiceMock.Received(1).NotifyBlockStatusUpdateAsync(blockId, BlockStatusCode.Error);
    }

    [Fact]
    public async Task EnqueueOrExecuteAtomicOperationsAsync_当Block不存在时_应返回NotFound()
    {
        // Arrange
        var blockId = "not-found-block";
        var operations = CreateSampleOperations();
        // 模拟 BlockManager 返回 null
         _blockManagerMock.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations)
             .Returns(Task.FromResult<(OneOf<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus>? blockStatus, List<OperationResult>? results)>((null, null)));


        // Act
        var result = await _sut.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);

        // Assert
        result.Should().Be(AtomicExecutionResult.NotFound);
        await _blockManagerMock.Received(1).EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);
        await _notifierServiceMock.DidNotReceive().NotifyBlockStatusUpdateAsync(Arg.Any<string>(), Arg.Any<BlockStatusCode>());
        await _notifierServiceMock.DidNotReceive().NotifyStateUpdateAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>());
    }

    // --- 测试 UpdateBlockGameStateAsync ---

    [Fact]
    public async Task UpdateBlockGameStateAsync_当Block存在时_应调用Manager并返回Success()
    {
        // Arrange
        var blockId = "update-gs-block";
        var settings = new Dictionary<string, object?> { { "key", "value" } };
        _blockManagerMock.UpdateBlockGameStateAsync(blockId, settings)
                         .Returns(Task.FromResult(UpdateResult.Success));

        // Act
        var result = await _sut.UpdateBlockGameStateAsync(blockId, settings);

        // Assert
        result.Should().Be(UpdateResult.Success);
        await _blockManagerMock.Received(1).UpdateBlockGameStateAsync(blockId, settings);
    }

    [Fact]
    public async Task UpdateBlockGameStateAsync_当Block不存在时_应调用Manager并返回NotFound()
    {
        // Arrange
        var blockId = "update-gs-not-found";
        var settings = new Dictionary<string, object?> { { "key", "value" } };
        _blockManagerMock.UpdateBlockGameStateAsync(blockId, settings)
                         .Returns(Task.FromResult(UpdateResult.NotFound));

        // Act
        var result = await _sut.UpdateBlockGameStateAsync(blockId, settings);

        // Assert
        result.Should().Be(UpdateResult.NotFound);
        await _blockManagerMock.Received(1).UpdateBlockGameStateAsync(blockId, settings);
    }

    // --- 测试 ApplyResolvedCommandsAsync ---

    [Fact]
    public async Task ApplyResolvedCommandsAsync_当BlockManager成功时_应通知更新()
    {
        // Arrange
        var blockId = "resolve-conflict-block";
        var resolvedCommands = CreateSampleOperations();
        var idleStatus = Block.CreateBlock(blockId, null, new WorldState(), new GameState()).ForceIdleState();
        var results = CreateSampleResults(resolvedCommands);
        OneOf<IdleBlockStatus, ErrorBlockStatus> statusOneOf = idleStatus; // 返回类型是 OneOf<Idle, Error>
         _blockManagerMock.ApplyResolvedCommandsAsync(blockId, resolvedCommands)
             .Returns(Task.FromResult<(OneOf<IdleBlockStatus, ErrorBlockStatus>? blockStatus, List<OperationResult>? results)>((statusOneOf, results)));


        // Act
        await _sut.ApplyResolvedCommandsAsync(blockId, resolvedCommands);

        // Assert
        await _blockManagerMock.Received(1).ApplyResolvedCommandsAsync(blockId, resolvedCommands);
        await _notifierServiceMock.Received(1).NotifyBlockStatusUpdateAsync(blockId, BlockStatusCode.Idle);
        await _notifierServiceMock.Received(1).NotifyStateUpdateAsync(blockId, Arg.Is<IEnumerable<string>>(ids => ids.Count() == 2));
    }

     [Fact]
    public async Task ApplyResolvedCommandsAsync_当BlockManager返回Error时_应通知更新但标记为Error()
    {
        // Arrange
        var blockId = "resolve-conflict-error";
        var resolvedCommands = CreateSampleOperations();
        var errorStatus = new ErrorBlockStatus(Block.CreateBlock(blockId, null, new WorldState(), new GameState()).Block);
        var results = CreateSampleResults(resolvedCommands, false); // 假设有失败结果
        OneOf<IdleBlockStatus, ErrorBlockStatus> statusOneOf = errorStatus;
        _blockManagerMock.ApplyResolvedCommandsAsync(blockId, resolvedCommands)
                         .Returns(Task.FromResult<(OneOf<IdleBlockStatus, ErrorBlockStatus>? blockStatus, List<OperationResult>? results)>((statusOneOf, results)));


        // Act
        await _sut.ApplyResolvedCommandsAsync(blockId, resolvedCommands);

        // Assert
        await _blockManagerMock.Received(1).ApplyResolvedCommandsAsync(blockId, resolvedCommands);
        // 仍然需要通知状态变化为 Error
        await _notifierServiceMock.Received(1).NotifyBlockStatusUpdateAsync(blockId, BlockStatusCode.Error);
        // 即使失败，也可能需要通知哪些实体尝试被修改了
        await _notifierServiceMock.Received(1).NotifyStateUpdateAsync(blockId, Arg.Is<IEnumerable<string>>(ids => ids.Count() == 2));
    }

    [Fact]
    public async Task ApplyResolvedCommandsAsync_当BlockManager返回Null时_应不通知()
    {
        // Arrange
        var blockId = "resolve-conflict-null";
        var resolvedCommands = CreateSampleOperations();
         _blockManagerMock.ApplyResolvedCommandsAsync(blockId, resolvedCommands)
             .Returns(Task.FromResult<(OneOf<IdleBlockStatus, ErrorBlockStatus>? blockStatus, List<OperationResult>? results)>((null, null)));


        // Act
        await _sut.ApplyResolvedCommandsAsync(blockId, resolvedCommands);

        // Assert
        await _blockManagerMock.Received(1).ApplyResolvedCommandsAsync(blockId, resolvedCommands);
        await _notifierServiceMock.DidNotReceive().NotifyBlockStatusUpdateAsync(Arg.Any<string>(), Arg.Any<BlockStatusCode>());
        await _notifierServiceMock.DidNotReceive().NotifyStateUpdateAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>());
    }

    // --- 测试 CreateChildBlockAsync ---

    [Fact]
    public async Task CreateChildBlockForWorkflowAsync_当Manager成功时_应返回Block并通知()
    {
        // Arrange
        var parentBlockId = "parent-for-workflow";
        var triggerParams = new Dictionary<string, object?> { { "workflow", "test" } };
        var newBlockId = "new-workflow-block";
        var newBlock = Block.CreateBlock(newBlockId, parentBlockId, new WorldState(), new GameState()); // Loading status
        _blockManagerMock.CreateChildBlock_Async(parentBlockId, triggerParams)
                         .Returns(Task.FromResult<LoadingBlockStatus?>(newBlock));

        // Act
        var result = await _sut.CreateChildBlockAsync(parentBlockId, triggerParams);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(newBlock);
        await _blockManagerMock.Received(1).CreateChildBlock_Async(parentBlockId, triggerParams);
        await _notifierServiceMock.Received(1).NotifyBlockStatusUpdateAsync(newBlockId, BlockStatusCode.Loading);
        // 可以在这里添加对父Block状态更新的通知（如果需要）
        // await _notifierServiceMock.Received(1).NotifyBlockStatusUpdateAsync(parentBlockId, Arg.Any<BlockStatusCode>());
    }

    [Fact]
    public async Task CreateChildBlockForWorkflowAsync_当Manager失败时_应返回Null且不通知()
    {
        // Arrange
        var parentBlockId = "parent-workflow-fail";
        var triggerParams = new Dictionary<string, object?> { { "workflow", "fail" } };
        _blockManagerMock.CreateChildBlock_Async(parentBlockId, triggerParams)
                         .Returns(Task.FromResult<LoadingBlockStatus?>(null));

        // Act
        var result = await _sut.CreateChildBlockAsync(parentBlockId, triggerParams);

        // Assert
        result.Should().BeNull();
        await _blockManagerMock.Received(1).CreateChildBlock_Async(parentBlockId, triggerParams);
        await _notifierServiceMock.DidNotReceive().NotifyBlockStatusUpdateAsync(Arg.Any<string>(), Arg.Any<BlockStatusCode>());
    }

    // --- 测试 HandleWorkflowCompletionAsync ---

    [Fact]
    public async Task HandleWorkflowCompletionAsync_当成功且无冲突时_应调用Manager()
    {
        // Arrange
        var blockId = "complete-workflow-success";
        var requestId = "req-success";
        var rawText = "成功内容";
        var commands = CreateSampleOperations();
        var outputVars = new Dictionary<string, object?>();
        var idleStatus = Block.CreateBlock(blockId, null, new WorldState(), new GameState()).ForceIdleState();
        var results = CreateSampleResults(commands);
        // 模拟 Manager 返回成功状态 (T0 of the outer OneOf)
        OneOf<IdleBlockStatus, ErrorBlockStatus> innerStatus = idleStatus;
        var managerResult = OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results), ConflictBlockStatus, ErrorBlockStatus>
            .FromT0((innerStatus, results));
        _blockManagerMock.HandleWorkflowCompletionAsync(blockId, true, rawText, commands, outputVars)
                         .Returns(Task.FromResult<OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results), ConflictBlockStatus, ErrorBlockStatus>?>(managerResult));

        // Act
        await _sut.HandleWorkflowCompletionAsync(blockId, requestId, true, rawText, commands, outputVars);

        // Assert
        await _blockManagerMock.Received(1).HandleWorkflowCompletionAsync(blockId, true, rawText, commands, outputVars);
        // 在这种情况下，writ service 不直接通知，manager 内部处理状态转换后由其他流程通知（或writ service 的其他方法通知）
        await _notifierServiceMock.DidNotReceive().NotifyConflictDetectedAsync(Arg.Any<ConflictDetectedDto>());
    }

    [Fact]
    public async Task HandleWorkflowCompletionAsync_当成功但有冲突时_应调用Manager并通知冲突()
    {
        // Arrange
        var blockId = "complete-workflow-conflict";
        var requestId = "req-conflict";
        var rawText = "冲突内容";
        var aiCommands = CreateSampleOperations();
        var userCommands = new List<AtomicOperation> { AtomicOperation.Modify(EntityType.Character, "char-op-1", "hp", Operator.Add, 5) }; // 假设用户修改了同一属性
        var outputVars = new Dictionary<string, object?>();

        // 创建模拟的 ConflictBlockStatus
        var conflictingAi = aiCommands.Where(c => c.EntityId == "char-op-1" && c.AttributeKey == "hp").ToList();
        var conflictingUser = userCommands.Where(c => c.EntityId == "char-op-1" && c.AttributeKey == "hp").ToList();
        var conflictStatus = new ConflictBlockStatus(
            Block.CreateBlock(blockId, null, new WorldState(), new GameState()).Block, // 基础 Block
            conflictingAi,
            conflictingUser,
            aiCommands,
            userCommands
        );

        // 模拟 Manager 返回 Conflict 状态 (T1 of the outer OneOf)
        var managerResult = OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results), ConflictBlockStatus, ErrorBlockStatus>
            .FromT1(conflictStatus);
        _blockManagerMock.HandleWorkflowCompletionAsync(blockId, true, rawText, aiCommands, outputVars) // 假设参数只传 AI 指令
                         .Returns(Task.FromResult<OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results), ConflictBlockStatus, ErrorBlockStatus>?>(managerResult));


        // Act
        await _sut.HandleWorkflowCompletionAsync(blockId, requestId, true, rawText, aiCommands, outputVars);

        // Assert
        await _blockManagerMock.Received(1).HandleWorkflowCompletionAsync(blockId, true, rawText, aiCommands, outputVars);
        // 验证冲突通知被发送
        await _notifierServiceMock.Received(1).NotifyConflictDetectedAsync(Arg.Is<ConflictDetectedDto>(dto =>
            dto.BlockId == blockId &&
            dto.RequestId == requestId &&
            dto.AiCommands.ToAtomicOperations().SequenceEqual(aiCommands) &&
            dto.UserCommands.ToAtomicOperations().SequenceEqual(userCommands) && // 验证是否正确传递了所有命令
            dto.ConflictingAiCommands.ToAtomicOperations().SequenceEqual(conflictingAi) &&
            dto.ConflictingUserCommands.ToAtomicOperations().SequenceEqual(conflictingUser)
        ));
    }

     [Fact]
    public async Task HandleWorkflowCompletionAsync_当失败时_应调用Manager()
    {
        // Arrange
        var blockId = "complete-workflow-fail";
        var requestId = "req-fail";
        var rawText = "失败内容";
        var commands = CreateSampleOperations();
        var outputVars = new Dictionary<string, object?>();
        var errorStatus = new ErrorBlockStatus(Block.CreateBlock(blockId, null, new WorldState(), new GameState()).Block);

        // 模拟 Manager 返回 Error 状态 (T2 of the outer OneOf)
         var managerResult = OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results), ConflictBlockStatus, ErrorBlockStatus>
             .FromT2(errorStatus);
         _blockManagerMock.HandleWorkflowCompletionAsync(blockId, false, rawText, commands, outputVars)
                          .Returns(Task.FromResult<OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results), ConflictBlockStatus, ErrorBlockStatus>?>(managerResult));

        // Act
        await _sut.HandleWorkflowCompletionAsync(blockId, requestId, false, rawText, commands, outputVars);

        // Assert
        await _blockManagerMock.Received(1).HandleWorkflowCompletionAsync(blockId, false, rawText, commands, outputVars);
        await _notifierServiceMock.DidNotReceive().NotifyConflictDetectedAsync(Arg.Any<ConflictDetectedDto>());
    }


    [Fact]
    public async Task HandleWorkflowCompletionAsync_当Manager返回Null时_应不执行任何操作()
    {
        // Arrange
        var blockId = "complete-workflow-null";
        var requestId = "req-null";
        var rawText = "空内容";
        var commands = CreateSampleOperations();
        var outputVars = new Dictionary<string, object?>();
        // 模拟 Manager 返回 null
         _blockManagerMock.HandleWorkflowCompletionAsync(blockId, true, rawText, commands, outputVars)
                          .Returns(Task.FromResult<OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results), ConflictBlockStatus, ErrorBlockStatus>?>(null));


        // Act
        await _sut.HandleWorkflowCompletionAsync(blockId, requestId, true, rawText, commands, outputVars);

        // Assert
        await _blockManagerMock.Received(1).HandleWorkflowCompletionAsync(blockId, true, rawText, commands, outputVars);
        await _notifierServiceMock.DidNotReceive().NotifyConflictDetectedAsync(Arg.Any<ConflictDetectedDto>());
        await _notifierServiceMock.DidNotReceive().NotifyBlockStatusUpdateAsync(Arg.Any<string>(), Arg.Any<BlockStatusCode>());
        await _notifierServiceMock.DidNotReceive().NotifyStateUpdateAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>());
    }
}