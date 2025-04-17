// --- START OF FILE YAESandBox.Tests/API/DTOs/DtoTests.cs ---

using FluentAssertions;
using System.Collections.Generic;
using Xunit;
using YAESandBox.API.DTOs;
using YAESandBox.Core.Action; // For AtomicOperation, Operator
using YAESandBox.Core.Block; // For BlockStatusCode
using YAESandBox.Core.State.Entity; // For EntityType

namespace YAESandBox.Tests.API.DTOs;

public class DtoTests
{
    [Fact]
    public void AtomicOperationRequestDto_应能正确设置和获取属性()
    {
        // Arrange
        var dto = new AtomicOperationRequestDto
        {
            OperationType = "CreateEntity",
            EntityType = EntityType.Character,
            EntityId = "char1",
            InitialAttributes = new Dictionary<string, object?> { { "name", "英雄" } },
            AttributeKey = null, // Not applicable for create
            ModifyOperator = null, // Not applicable for create
            ModifyValue = null // Not applicable for create
        };

        // Act & Assert
        dto.OperationType.Should().Be("CreateEntity");
        dto.EntityType.Should().Be(EntityType.Character);
        dto.EntityId.Should().Be("char1");
        dto.InitialAttributes.Should().ContainKey("name").WhoseValue.Should().Be("英雄");
        dto.AttributeKey.Should().BeNull();
        dto.ModifyOperator.Should().BeNull();
        dto.ModifyValue.Should().BeNull();
    }

    [Fact]
    public void BatchAtomicRequestDto_应能正确设置和获取操作列表()
    {
        // Arrange
        var op1 = new AtomicOperationRequestDto { OperationType = "CreateEntity", EntityType = EntityType.Item, EntityId = "item1" };
        var op2 = new AtomicOperationRequestDto { OperationType = "ModifyEntity", EntityType = EntityType.Item, EntityId = "item1", AttributeKey = "desc", ModifyOperator = "+=", ModifyValue = "新的描述" };
        var dtoList = new List<AtomicOperationRequestDto> { op1, op2 };
        var dto = new BatchAtomicRequestDto
        {
            Operations = dtoList
        };

        // Act & Assert
        dto.Operations.Should().NotBeNull();
        dto.Operations.Should().HaveCount(2);
        dto.Operations.Should().Contain(op1);
        dto.Operations.Should().Contain(op2);
    }

    [Fact]
    public void BlockDetailDto_应能正确设置和获取属性()
    {
        // Arrange
        var dto = new BlockDetailDto
        {
            BlockId = "blk1",
            ParentBlockId = "blk0",
            StatusCode = BlockStatusCode.Idle,
            BlockContent = "这是内容",
            Metadata = new Dictionary<string, string> { { "creator", "test" } },
            ChildrenInfo = new List<string> { "blk2" }
        };

        // Act & Assert
        dto.BlockId.Should().Be("blk1");
        dto.ParentBlockId.Should().Be("blk0");
        dto.StatusCode.Should().Be(BlockStatusCode.Idle);
        dto.BlockContent.Should().Be("这是内容");
        dto.Metadata.Should().ContainKey("creator").WhoseValue.Should().Be("test");
        dto.ChildrenInfo.Should().ContainSingle().Which.Should().Be("blk2");
    }

    [Fact]
    public void ConflictDetectedDto_应能正确设置和获取属性()
    {
        // Arrange
        var aiCmd = AtomicOperation.Create(EntityType.Item, "item1");
        var userCmd = AtomicOperation.Modify(EntityType.Item, "item1", "name", "=", "新名字");
        var dto = new ConflictDetectedDto
        {
            RequestId = "req123",
            BlockId = "blkConflict",
            AiCommands = new List<AtomicOperation> { aiCmd },
            UserCommands = new List<AtomicOperation> { userCmd },
            ConflictingAiCommands = new List<AtomicOperation> { aiCmd }, // 示例
            ConflictingUserCommands = new List<AtomicOperation> { userCmd } // 示例
        };

        // Act & Assert
        dto.RequestId.Should().Be("req123");
        dto.BlockId.Should().Be("blkConflict");
        dto.AiCommands.Should().ContainSingle().Which.Should().Be(aiCmd);
        dto.UserCommands.Should().ContainSingle().Which.Should().Be(userCmd);
        dto.ConflictingAiCommands.Should().ContainSingle().Which.Should().Be(aiCmd);
        dto.ConflictingUserCommands.Should().ContainSingle().Which.Should().Be(userCmd);
    }

    [Fact]
    public void EntitySummaryDto_应能正确设置和获取属性()
    {
        // Arrange
        var dto = new EntitySummaryDto
        {
            EntityId = "place1",
            EntityType = EntityType.Place,
            IsDestroyed = false,
            Name = "城堡"
        };

        // Act & Assert
        dto.EntityId.Should().Be("place1");
        dto.EntityType.Should().Be(EntityType.Place);
        dto.IsDestroyed.Should().BeFalse();
        dto.Name.Should().Be("城堡");
    }

     [Fact]
    public void EntityDetailDto_应能正确设置和获取属性包括Attributes()
    {
        // Arrange
        var attributes = new Dictionary<string, object?> { { "name", "宝剑" }, { "damage", 10 } };
        var dto = new EntityDetailDto
        {
            EntityId = "item_sword",
            EntityType = EntityType.Item,
            IsDestroyed = false,
            Name = "宝剑", // 从 Attributes 映射或单独设置
            Attributes = attributes
        };

        // Act & Assert
        dto.EntityId.Should().Be("item_sword");
        dto.EntityType.Should().Be(EntityType.Item);
        dto.IsDestroyed.Should().BeFalse();
        dto.Name.Should().Be("宝剑");
        dto.Attributes.Should().BeEquivalentTo(attributes); // 比较字典内容
    }


    [Fact]
    public void GameStateDto_应能正确设置和获取Settings()
    {
        // Arrange
        var settings = new Dictionary<string, object?> { { "difficulty", "hard" }, { "time", 120 } };
        var dto = new GameStateDto
        {
            Settings = settings
        };

        // Act & Assert
        dto.Settings.Should().NotBeNull();
        dto.Settings.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public void ResolveConflictRequestDto_应能正确设置和获取属性()
    {
        // Arrange
        var resolvedCmd = AtomicOperation.Create(EntityType.Character, "char_new", new() { { "name", "最终角色" } });
        var dto = new ResolveConflictRequestDto
        {
            RequestId = "req123",
            BlockId = "blkConflict",
            ResolvedCommands = new List<AtomicOperation> { resolvedCmd }
        };

        // Act & Assert
        dto.RequestId.Should().Be("req123");
        dto.BlockId.Should().Be("blkConflict");
        dto.ResolvedCommands.Should().ContainSingle().Which.Should().Be(resolvedCmd);
    }

     [Fact]
    public void StateUpdateSignalDto_应能正确设置和获取BlockId()
    {
        // Arrange
        var dto = new StateUpdateSignalDto
        {
            BlockId = "blkUpdated"
        };

        // Act & Assert
        dto.BlockId.Should().Be("blkUpdated");
    }


    [Fact]
    public void TriggerWorkflowRequestDto_应能正确设置和获取属性()
    {
        // Arrange
        var parameters = new Dictionary<string, object?> { { "target", "char1" }, { "intensity", 5 } };
        var dto = new TriggerWorkflowRequestDto
        {
            RequestId = "reqTrigger",
            ParentBlockId = "blkParent",
            WorkflowName = "AttackWorkflow",
            Params = parameters
        };

        // Act & Assert
        dto.RequestId.Should().Be("reqTrigger");
        dto.ParentBlockId.Should().Be("blkParent");
        dto.WorkflowName.Should().Be("AttackWorkflow");
        dto.Params.Should().BeEquivalentTo(parameters);
    }

    [Fact]
    public void UpdateGameStateRequestDto_应能正确设置和获取SettingsToUpdate()
    {
        // Arrange
         var settings = new Dictionary<string, object?> { { "weather", "rainy" }};
        var dto = new UpdateGameStateRequestDto
        {
            SettingsToUpdate = settings
        };

        // Act & Assert
        dto.SettingsToUpdate.Should().NotBeNull();
        dto.SettingsToUpdate.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public void WorkflowCompleteDto_应能正确设置和获取属性()
    {
        // Arrange
        var dtoSuccess = new WorkflowCompleteDto
        {
            RequestId = "reqComplete",
            BlockId = "blkFinished",
            ExecutionStatus = "success",
            FinalContent = "任务成功完成。",
            ErrorMessage = null
        };
         var dtoFail = new WorkflowCompleteDto
        {
            RequestId = "reqFail",
            BlockId = "blkError",
            ExecutionStatus = "failure",
            FinalContent = null,
            ErrorMessage = "发生未知错误"
        };

        // Act & Assert Success
        dtoSuccess.RequestId.Should().Be("reqComplete");
        dtoSuccess.BlockId.Should().Be("blkFinished");
        dtoSuccess.ExecutionStatus.Should().Be("success");
        dtoSuccess.FinalContent.Should().Be("任务成功完成。");
        dtoSuccess.ErrorMessage.Should().BeNull();

        // Act & Assert Failure
        dtoFail.RequestId.Should().Be("reqFail");
        dtoFail.BlockId.Should().Be("blkError");
        dtoFail.ExecutionStatus.Should().Be("failure");
        dtoFail.FinalContent.Should().BeNull();
        dtoFail.ErrorMessage.Should().Be("发生未知错误");
    }

    [Fact]
    public void WorkflowUpdateDto_应能正确设置和获取属性()
    {
        // Arrange
        var dto = new WorkflowUpdateDto
        {
            RequestId = "reqUpdate",
            BlockId = "blkProgress",
            UpdateType = "stream_chunk",
            Data = "正在生成..."
        };

        // Act & Assert
        dto.RequestId.Should().Be("reqUpdate");
        dto.BlockId.Should().Be("blkProgress");
        dto.UpdateType.Should().Be("stream_chunk");
        dto.Data.Should().Be("正在生成...");
    }
}

// --- END OF FILE YAESandBox.Tests/API/DTOs/DtoTests.cs ---