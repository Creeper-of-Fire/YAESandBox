using Xunit;
using FluentAssertions;
using NSubstitute;
using Microsoft.AspNetCore.SignalR;
using YAESandBox.API.Services;
using YAESandBox.API.Hubs;
using YAESandBox.API.DTOs;
using YAESandBox.Core.Block; // For BlockStatusCode
using YAESandBox.Core.Action; // For AtomicOperation
using System.Collections.Generic;
using System.Threading.Tasks;
using YAESandBox.Core.State.Entity;

namespace YAESandBox.Tests.API.Services;

public class SignalRNotifierServiceTests
{
    private readonly IHubContext<GameHub, IGameClient> _hubContextMock;
    private readonly IHubClients<IGameClient> _clientsMock;
    private readonly IGameClient _gameClientMock; // Mock a specific client/group (e.g., All)
    private readonly SignalRNotifierService _sut;

    public SignalRNotifierServiceTests()
    {
        _hubContextMock = Substitute.For<IHubContext<GameHub, IGameClient>>();
        _clientsMock = Substitute.For<IHubClients<IGameClient>>();
        _gameClientMock = Substitute.For<IGameClient>();

        // 设置模拟链: HubContext -> Clients -> All -> IGameClient methods
        _hubContextMock.Clients.Returns(_clientsMock);
        _clientsMock.All.Returns(_gameClientMock); // 模拟向所有客户端发送

        _sut = new SignalRNotifierService(_hubContextMock);
    }

    [Fact]
    public async Task NotifyBlockStatusUpdateAsync_应调用ClientsAllReceiveBlockStatusUpdate()
    {
        // Arrange
        var blockId = "block-status-update";
        var newStatusCode = BlockStatusCode.Idle;

        // Act
        await _sut.NotifyBlockStatusUpdateAsync(blockId, newStatusCode);

        // Assert
        // 验证 Clients.All.ReceiveBlockStatusUpdate 被调用了一次，并带有正确的参数
        await _gameClientMock.Received(1).ReceiveBlockStatusUpdate(Arg.Is<BlockStatusUpdateDto>(dto =>
            dto.BlockId == blockId &&
            dto.StatusCode == newStatusCode
            // dto.ParentBlockId == null // 当前实现无法获取 ParentId，所以验证其为 null
        ));
    }

    [Fact]
    public async Task NotifyStateUpdateAsync_应调用ClientsAllReceiveStateUpdateSignal()
    {
        // Arrange
        var blockId = "state-update-signal";
        var changedIds = new List<string> { "entity-1", "entity-2" };

        // Act
        await _sut.NotifyStateUpdateAsync(blockId, changedIds);

        // Assert
        await _gameClientMock.Received(1).ReceiveStateUpdateSignal(Arg.Is<StateUpdateSignalDto>(dto =>
            dto.BlockId == blockId
            // 当前实现未包含 ChangedEntityIds，如果添加了，需要在此处验证
            // && dto.ChangedEntityIds.SequenceEqual(changedIds)
        ));
    }

     [Fact]
    public async Task NotifyStateUpdateAsync_当无变更ID时_应调用ClientsAllReceiveStateUpdateSignal()
    {
        // Arrange
        var blockId = "state-update-no-ids";

        // Act
        await _sut.NotifyStateUpdateAsync(blockId); // 不传递 changedEntityIds

        // Assert
        await _gameClientMock.Received(1).ReceiveStateUpdateSignal(Arg.Is<StateUpdateSignalDto>(dto =>
            dto.BlockId == blockId
            // && dto.ChangedEntityIds.Count == 0 // 如果添加了，验证为空
        ));
    }

    [Fact]
    public async Task NotifyWorkflowUpdateAsync_应调用ClientsAllReceiveWorkflowUpdate()
    {
        // Arrange
        var updateDto = new WorkflowUpdateDto
        {
            RequestId = "wf-update-req",
            BlockId = "wf-update-block",
            UpdateType = "stream_chunk",
            Data = "一些流式文本..."
        };

        // Act
        await _sut.NotifyWorkflowUpdateAsync(updateDto);

        // Assert
        // 直接验证传递的对象是否与预期相同
        await _gameClientMock.Received(1).ReceiveWorkflowUpdate(updateDto);
    }

    [Fact]
    public async Task NotifyWorkflowCompleteAsync_应调用ClientsAllReceiveWorkflowComplete()
    {
        // Arrange
        var completeDto = new WorkflowCompleteDto
        {
            RequestId = "wf-complete-req",
            BlockId = "wf-complete-block",
            ExecutionStatus = "success",
            FinalContent = "最终结果摘要"
        };

        // Act
        await _sut.NotifyWorkflowCompleteAsync(completeDto);

        // Assert
        await _gameClientMock.Received(1).ReceiveWorkflowComplete(completeDto);
    }

    [Fact]
    public async Task NotifyConflictDetectedAsync_应调用ClientsAllReceiveConflictDetected()
    {
        // Arrange
        var conflictDto = new ConflictDetectedDto
        {
            RequestId = "wf-conflict-req",
            BlockId = "wf-conflict-block",
            AiCommands = [AtomicOperation.Modify(EntityType.Item, "item-c", "value", Operator.Equal, 10)],
            UserCommands = [AtomicOperation.Modify(EntityType.Item, "item-c", "value", Operator.Equal, 20)],
            ConflictingAiCommands = [AtomicOperation.Modify(EntityType.Item, "item-c", "value", Operator.Equal, 10)],
            ConflictingUserCommands = [AtomicOperation.Modify(EntityType.Item, "item-c", "value", Operator.Equal, 20)]
        };

        // Act
        await _sut.NotifyConflictDetectedAsync(conflictDto);

        // Assert
        await _gameClientMock.Received(1).ReceiveConflictDetected(conflictDto);
    }
}