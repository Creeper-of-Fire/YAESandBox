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
using System.Threading.Tasks;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReceivedExtensions; // for ThrowAsync

namespace YAESandBox.Tests.API.Services;

public class WorkflowServiceTests
{
    private readonly IBlockReadService _blockReadServiceMock;
    private readonly IBlockWritService _blockWritServiceMock;
    private readonly INotifierService _notifierServiceMock;
    private readonly WorkflowService _sut;

    public WorkflowServiceTests()
    {
        _blockReadServiceMock = Substitute.For<IBlockReadService>();
        _blockWritServiceMock = Substitute.For<IBlockWritService>();
        _notifierServiceMock = Substitute.For<INotifierService>();
        _sut = new WorkflowService(_blockReadServiceMock, _blockWritServiceMock, _notifierServiceMock);

        // 配置日志记录器（如果需要验证日志输出）
        // Log.Logger = Substitute.For<ILogger>(); // 需要修改 Log 类以支持注入或静态设置
    }

    private TriggerWorkflowRequestDto CreateTriggerRequest(string parentId = "parent-1", string workflow = "test-wf",
        string reqId = "req-1") =>
        new()
        {
            RequestId = reqId,
            ParentBlockId = parentId,
            WorkflowName = workflow,
            Params = new Dictionary<string, object?> { { "param1", "value1" } }
        };

    private ResolveConflictRequestDto
        CreateResolveRequest(string blockId = "block-conflict", string reqId = "req-c-1") =>
        new()
        {
            RequestId = reqId,
            BlockId = blockId,
            ResolvedCommands = [AtomicOperation.Create(EntityType.Item, "resolved-item")]
        };

    [Fact]
    public async Task HandleWorkflowTriggerAsync_成功时_应创建Block并启动后台执行()
    {
        // Arrange
        var request = CreateTriggerRequest();
        var newBlockId = "new-block-wf";
        var newBlock = Block.CreateBlock(newBlockId, request.ParentBlockId, new WorldState(), new GameState());
        // 假设你使用的是 CreateChildBlockAsync
        _blockWritServiceMock.CreateChildBlockAsync(request.ParentBlockId, request.Params)
            .Returns(Task.FromResult<LoadingBlockStatus?>(newBlock));

        // Act
        await _sut.HandleWorkflowTriggerAsync(request);

        // Assert
        // 1. **只验证** CreateChildBlockAsync 被调用
        await _blockWritServiceMock.Received(1).CreateChildBlockAsync(request.ParentBlockId, request.Params);

        // 2. (可选) 验证相关日志
        // Log.Received(1).Info($"为工作流 '{request.WorkflowName}' 创建了新的子 Block: {newBlock.Block.BlockId}");

        // 3. **彻底移除对后台任务内部调用的所有验证 (包括 DidNotReceive)**
        // 因为无法可靠地预测后台任务执行到哪一步
    }

    [Fact]
    public async Task StartExecuteWorkflowAsync_成功时_应执行完整流程并通知()
    {
        // Arrange
        var request = CreateTriggerRequest();
        var blockId = "exec-wf-block";

        // 模拟 ExecuteWorkflowAsync 内部最终会调用的方法
        // 假设工作流成功完成
        _blockWritServiceMock.HandleWorkflowCompletionAsync(blockId, request.RequestId, true, Arg.Any<string>(),
                Arg.Is<List<AtomicOperation>>(cmds => cmds.Count > 0), Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.CompletedTask);
        // 模拟通知调用
        _notifierServiceMock.NotifyWorkflowUpdateAsync(Arg.Any<WorkflowUpdateDto>()).Returns(Task.CompletedTask);
        _notifierServiceMock.NotifyWorkflowCompleteAsync(Arg.Any<WorkflowCompleteDto>()).Returns(Task.CompletedTask);

        // Act
        // *** 直接调用 StartExecuteWorkflowAsync ***
        await _sut.StartExecuteWorkflowAsync(request, blockId);

        // Assert
        // 验证 ExecuteWorkflowAsync 内部的预期调用顺序和参数
        // 1. 流式更新应该被调用多次（至少一次）
        await _notifierServiceMock.ReceivedWithAnyArgs(Quantity.AtLeastOne()).NotifyWorkflowUpdateAsync(
            Arg.Is<WorkflowUpdateDto>(dto =>
                    dto.RequestId == request.RequestId &&
                    dto.BlockId == blockId &&
                    dto.UpdateType == "stream_chunk" // 验证类型
            ));

        // 2. HandleWorkflowCompletionAsync 应该在最后被调用一次，且 success 为 true
        await _blockWritServiceMock.Received(1).HandleWorkflowCompletionAsync(
            blockId,
            request.RequestId,
            true, // Success = true
            Arg.Any<string>(), // Raw text (可以验证部分内容如果需要)
            Arg.Is<List<AtomicOperation>>(cmds => cmds.Any(op =>
                op.OperationType == AtomicOperationType.CreateEntity &&
                op.EntityId == "clumsy-knight")), // 验证是否包含模拟生成的指令
            Arg.Any<Dictionary<string, object?>>());

        // 3. NotifyWorkflowCompleteAsync 应该在 HandleWorkflowCompletionAsync 之后（或在 finally 中）被调用，且状态为 success
        await _notifierServiceMock.Received(1).NotifyWorkflowCompleteAsync(Arg.Is<WorkflowCompleteDto>(dto =>
            dto.RequestId == request.RequestId &&
            dto.BlockId == blockId &&
            dto.ExecutionStatus == "success" && // 验证成功
            dto.ErrorMessage == null
        ));
    }

    [Fact]
    public async Task StartExecuteWorkflowAsync_执行时发生异常_应记录错误并发送失败通知()
    {
        // Arrange
        var request = CreateTriggerRequest(workflow: "exception-wf");
        var blockId = "exec-wf-exc";
        var simulatedException = new InvalidOperationException("模拟工作流内部异常");

        // 模拟工作流内部（例如，某个 Notifier 调用）抛出异常
        _notifierServiceMock.NotifyWorkflowUpdateAsync(Arg.Any<WorkflowUpdateDto>())
            .ThrowsAsync(simulatedException); // 模拟流式更新时失败

        // 模拟 HandleWorkflowCompletionAsync 仍然会被调用（在 finally 块中），但 success 会是 false
        _blockWritServiceMock.HandleWorkflowCompletionAsync(blockId, request.RequestId, false, Arg.Any<string>(),
                Arg.Any<List<AtomicOperation>>(), Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.CompletedTask);
        // 模拟最终的失败通知
        _notifierServiceMock.NotifyWorkflowCompleteAsync(Arg.Any<WorkflowCompleteDto>()).Returns(Task.CompletedTask);


        // Act
        // 直接调用 StartExecuteWorkflowAsync，它内部会 catch 异常
        await _sut.StartExecuteWorkflowAsync(request, blockId);

        // Assert
        // 1. 验证失败的 NotifyWorkflowUpdateAsync 被调用了
        await _notifierServiceMock.ReceivedWithAnyArgs(1).NotifyWorkflowUpdateAsync(Arg.Any<WorkflowUpdateDto>());

        // 2. 验证 HandleWorkflowCompletionAsync 被调用，且 success 为 false
        await _blockWritServiceMock.Received(1).HandleWorkflowCompletionAsync(
            blockId,
            request.RequestId,
            false, // Success = false
            Arg.Is<string>(s => s.Contains("执行失败: " + simulatedException.Message)), // 验证 rawText 包含错误信息
            Arg.Is<List<AtomicOperation>>(cmds => cmds.Count == 0), // 异常发生时，指令列表可能为空
            Arg.Any<Dictionary<string, object?>>());

        // 3. 验证 NotifyWorkflowCompleteAsync 被调用，且状态为 failure
        await _notifierServiceMock.Received(1).NotifyWorkflowCompleteAsync(Arg.Is<WorkflowCompleteDto>(dto =>
                dto.RequestId == request.RequestId &&
                dto.BlockId == blockId &&
                dto.ExecutionStatus == "failure" && // 验证失败
                dto.ErrorMessage == $"工作流 '{request.WorkflowName}' 执行失败: {simulatedException.Message}" // 验证错误消息
        ));

        // 4. (可选) 验证错误日志记录
        // Log.Received(1).Error(simulatedException, Arg.Any<string>());
    }

    [Fact]
    public async Task HandleWorkflowTriggerAsync_创建Block失败时_应记录错误且不继续()
    {
        // Arrange
        var request = CreateTriggerRequest(parentId: "fail-parent");
        _blockWritServiceMock.CreateChildBlockAsync(request.ParentBlockId, request.Params)
            .Returns(Task.FromResult<LoadingBlockStatus?>(null)); // 模拟创建失败

        // Act
        await _sut.HandleWorkflowTriggerAsync(request);
        await Task.Delay(50); // 短暂等待以防 Task.Run 意外启动

        // Assert
        // 验证创建子 Block 的调用仍然发生
        await _blockWritServiceMock.Received(1).CreateChildBlockAsync(request.ParentBlockId, request.Params);
        // 验证后续步骤（如工作流完成处理）没有被调用
        await _blockWritServiceMock.DidNotReceiveWithAnyArgs()
            .HandleWorkflowCompletionAsync(default!, default!, default!, default!, default!, default!);
        await _notifierServiceMock.DidNotReceiveWithAnyArgs().NotifyWorkflowCompleteAsync(default!);
        await _notifierServiceMock.DidNotReceiveWithAnyArgs().NotifyWorkflowUpdateAsync(default!);
    }

    [Fact]
    // 重命名测试以反映其真实焦点：启动流程
    public async Task HandleWorkflowTriggerAsync_当创建Block成功时_应完成启动流程()
    {
        // Arrange
        var request = CreateTriggerRequest(workflow: "any-workflow"); // 工作流名称不重要
        var newBlockId = "new-block-start";
        var newBlock = Block.CreateBlock(newBlockId, request.ParentBlockId, new WorldState(), new GameState());
        // 模拟 CreateChildBlockAsync 成功
        _blockWritServiceMock.CreateChildBlockAsync(request.ParentBlockId, request.Params)
            .Returns(Task.FromResult<LoadingBlockStatus?>(newBlock));

        // Act
        await _sut.HandleWorkflowTriggerAsync(request);
        // 不需要 Task.Delay

        // Assert
        // 1. 验证 CreateChildBlockAsync 被调用
        await _blockWritServiceMock.Received(1).CreateChildBlockAsync(request.ParentBlockId, request.Params);

        // 2. 验证没有直接调用 HandleWorkflowCompletionAsync 或 Notify* (这些发生在后台)
        // 无法验证
        // await _blockWritServiceMock.DidNotReceiveWithAnyArgs()
        //     .HandleWorkflowCompletionAsync(default!, default!, default!, default!, default!, default!);
        // await _notifierServiceMock.DidNotReceiveWithAnyArgs().NotifyWorkflowCompleteAsync(default!);
        // await _notifierServiceMock.DidNotReceiveWithAnyArgs().NotifyWorkflowUpdateAsync(default!);
        // (可选) 可以验证 Info 日志被调用
    }

    [Fact]
    public async Task HandleConflictResolutionAsync_应调用WritService的ApplyResolvedCommandsAsync()
    {
        // Arrange
        var request = CreateResolveRequest();
        _blockWritServiceMock.ApplyResolvedCommandsAsync(request.BlockId, request.ResolvedCommands)
            .Returns(Task.CompletedTask); // 模拟调用成功

        // Act
        await _sut.HandleConflictResolutionAsync(request);

        // Assert
        await _blockWritServiceMock.Received(1).ApplyResolvedCommandsAsync(request.BlockId, request.ResolvedCommands);
    }
}