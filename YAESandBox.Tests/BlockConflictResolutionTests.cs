using Xunit;
using FluentAssertions;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Core.Action;
using System.Threading.Tasks;
using System.Collections.Generic;
using OneOf;
using OneOf.Types; // For OneOf types like Success

namespace YAESandBox.Core.Tests
{
    /// <summary>
    /// BlockManager 冲突解决和状态转换逻辑的单元测试。
    /// </summary>
    public class BlockConflictResolutionTests : IAsyncLifetime
    {
        private BlockManager _manager = null!;
        private string _parentId = BlockManager.WorldRootId;
        private string _loadingBlockId = string.Empty; // Will be set during setup
        private TypedID _targetEntityRef;

        // 初始化，创建 Manager 和一个处于 Loading 状态的 Block
        public async Task InitializeAsync()
        {
            _manager = new BlockManager();
            // 先在父节点创建实体，以便子节点可以修改它
            _targetEntityRef = new TypedID(EntityType.Character, "char_conflict_target");
            var createOp = AtomicOperation.Create(_targetEntityRef.Type, _targetEntityRef.Id,
                new Dictionary<string, object?> { { "健康", 100 } });
            await _manager.EnqueueOrExecuteAtomicOperationsAsync(_parentId, [createOp]);

            // 创建一个子 Block，它会处于 Loading 状态
            var loadingStatus =
                await _manager.CreateChildBlock_Async(_parentId, new Dictionary<string, object?> { { "触发", "测试冲突" } });
            loadingStatus.Should().NotBeNull();
            _loadingBlockId = loadingStatus!.Block.BlockId;

            // 确认 Block 确实是 Loading 状态
            var currentStatus = await _manager.GetBlockAsync(_loadingBlockId);
            currentStatus.Should().BeOfType<LoadingBlockStatus>();
        }

        public Task DisposeAsync()
        {
            // 清理 (如果需要)
            return Task.CompletedTask;
        }

        [Fact(DisplayName = "工作流完成_无冲突_应合并命令并进入Idle")]
        public async Task HandleWorkflowCompletion_NoConflict_ShouldMergeCommandsAndEnterIdle()
        {
            // Arrange
            var aiCommands = new List<AtomicOperation>
            {
                AtomicOperation.Modify(_targetEntityRef.Type, _targetEntityRef.Id, "法力", Operator.Equal, 50) // AI 设置法力
            };
            var userCommands = new List<AtomicOperation>
            {
                AtomicOperation.Modify(_targetEntityRef.Type, _targetEntityRef.Id, "姓名", Operator.Equal,
                    "用户改名") // 用户修改姓名
            };

            // 在 Loading 状态下加入用户指令 (这些会被暂存)
            var userEnqueueResult = await _manager.EnqueueOrExecuteAtomicOperationsAsync(_loadingBlockId, userCommands);
            userEnqueueResult.blockStatus.Should().NotBeNull();
            userEnqueueResult.blockStatus.Value.AsT1.Should().NotBeNull().And.BeOfType<LoadingBlockStatus>();
            userEnqueueResult.results.Should().NotBeNull().And.OnlyContain(r => r.Success);


            // Act: 模拟工作流成功完成，传入 AI 指令
            var completionResult = await _manager.HandleWorkflowCompletionAsync(
                _loadingBlockId,
                success: true,
                rawText: "AI 完成，无冲突",
                firstPartyCommands: aiCommands,
                outputVariables: new Dictionary<string, object?>());

            // Assert: 状态转换
            completionResult.Should().NotBeNull();
            completionResult.Value.Should()
                .BeAssignableTo<OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult>
                    results), ConflictBlockStatus, ErrorBlockStatus>>();

            // 检查结果是成功分支 (Idle 或 Error)
            completionResult.Value.IsT0.Should().BeTrue();
            var successTuple = completionResult.Value.AsT0;

            // 检查具体状态是 Idle
            successTuple.blockStatus.Should().BeAssignableTo<OneOf<IdleBlockStatus, ErrorBlockStatus>>();
            successTuple.blockStatus.IsT0.Should().BeTrue(); // 确认是 IdleBlockStatus
            successTuple.blockStatus.AsT0.Should().BeOfType<IdleBlockStatus>();

            // 检查 BlockManager 中的状态
            var finalStatus = await _manager.GetBlockAsync(_loadingBlockId);
            finalStatus.Should().BeOfType<IdleBlockStatus>();

            // Assert: WorldState 检查 (wsPostUser 应包含所有修改)
            var idleStatus = (IdleBlockStatus)finalStatus!;
            var finalWs = idleStatus.CurrentWorldState; // Idle 状态下 CurrentWorldState 是 wsPostUser
            finalWs.Should().NotBeNull();
            var targetEntity = finalWs.FindEntity(_targetEntityRef);
            targetEntity.Should().NotBeNull();
            targetEntity!.GetAttribute("法力").Should().Be(50L); // AI 的修改 (int -> long in JSON)
            targetEntity!.GetAttribute("姓名").Should().Be("用户改名"); // 用户的修改
            targetEntity!.GetAttribute("健康").Should().Be(100L); // 初始值应保留
        }

        [Fact(DisplayName = "工作流完成_有冲突_应进入Conflict")]
        public async Task HandleWorkflowCompletion_WithConflict_ShouldEnterConflictState()
        {
            // Arrange
            // 用户先修改健康
            var userCommands = new List<AtomicOperation>
            {
                AtomicOperation.Modify(_targetEntityRef.Type, _targetEntityRef.Id, "健康", Operator.Sub, 10) // 用户扣血
            };
            var userEnqueueResult = await _manager.EnqueueOrExecuteAtomicOperationsAsync(_loadingBlockId, userCommands);
            userEnqueueResult.blockStatus.Should().NotBeNull();
            userEnqueueResult.blockStatus.Value.AsT1.Should().NotBeNull().And.BeOfType<LoadingBlockStatus>();
            userEnqueueResult.results.Should().NotBeNull().And.OnlyContain(r => r.Success);


            // AI 也要修改健康 (产生冲突)
            var aiCommands = new List<AtomicOperation>
            {
                AtomicOperation.Modify(_targetEntityRef.Type, _targetEntityRef.Id, "健康", Operator.Add, 20) // AI 加血
            };

            // Act: 模拟工作流成功完成，传入冲突的 AI 指令
            var completionResult = await _manager.HandleWorkflowCompletionAsync(
                _loadingBlockId,
                success: true,
                rawText: "AI 完成，有冲突",
                firstPartyCommands: aiCommands,
                outputVariables: new Dictionary<string, object?>());

            // Assert: 状态转换
            completionResult.Should().NotBeNull();
            completionResult.Value.Should()
                .BeAssignableTo<OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult>
                    results), ConflictBlockStatus, ErrorBlockStatus>>();

            // 检查结果是冲突分支
            completionResult.Value.IsT1.Should().BeTrue();
            var conflictStatus = completionResult.Value.AsT1;
            conflictStatus.Should().BeOfType<ConflictBlockStatus>();

            // 检查 BlockManager 中的状态
            var finalStatus = await _manager.GetBlockAsync(_loadingBlockId);
            finalStatus.Should().BeOfType<ConflictBlockStatus>();

            // Assert: 冲突信息
            var actualConflictStatus = (ConflictBlockStatus)finalStatus!;
            actualConflictStatus.conflictingAiCommands.Should().BeEquivalentTo(aiCommands);
            actualConflictStatus.conflictingUserCommands.Should().BeEquivalentTo(userCommands); // 用户暂存的冲突指令
            actualConflictStatus.AiCommands.Should().BeEquivalentTo(aiCommands); // 完整的 AI 指令
            actualConflictStatus.UserCommands.Should().BeEquivalentTo(userCommands); // 完整的用户指令

            // Assert: WorldState 检查 (wsTemp 应只包含用户修改, wsPostAI/User 应为 null)
            actualConflictStatus.Block.wsPostAI.Should().BeNull();
            actualConflictStatus.Block.wsPostUser.Should().BeNull();
            actualConflictStatus.Block.wsTemp.Should().NotBeNull(); // wsTemp 仍然存在
            var tempWs = actualConflictStatus.CurrentWorldState; // Conflict 状态下 CurrentWorldState 是 wsTemp
            var targetEntity = tempWs.FindEntity(_targetEntityRef);
            targetEntity.Should().NotBeNull();
            targetEntity!.GetAttribute("健康").Should().Be(90L); // 只应用了用户的扣血 (100 - 10)
        }

        [Fact(DisplayName = "工作流完成_无冲突但AI命令失败_应进入Error")]
        public async Task HandleWorkflowCompletion_NoConflictButAiCommandFails_ShouldEnterErrorState()
        {
            // Arrange
            var aiCommands = new List<AtomicOperation>
            {
                AtomicOperation.Modify(EntityType.Item, "non_existent_item", "属性", Operator.Equal,
                    "value") // 对不存在的实体操作，会失败
            };
            var userCommands = new List<AtomicOperation>
            {
                AtomicOperation.Modify(_targetEntityRef.Type, _targetEntityRef.Id, "姓名", Operator.Equal,
                    "另一个名字") // 无冲突的用户指令
            };

            // 暂存用户指令
            await _manager.EnqueueOrExecuteAtomicOperationsAsync(_loadingBlockId, userCommands);

            // Act
            var completionResult = await _manager.HandleWorkflowCompletionAsync(
                _loadingBlockId,
                success: true, // 工作流本身成功，但内部指令失败
                rawText: "AI 完成，但指令失败",
                firstPartyCommands: aiCommands,
                outputVariables: new Dictionary<string, object?>());


            // Assert: 状态转换
            completionResult.Should().NotBeNull();
            completionResult.Value.Should()
                .BeAssignableTo<OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult>
                    results), ConflictBlockStatus, ErrorBlockStatus>>();

            // 检查结果是成功分支 (Idle 或 Error)
            completionResult.Value.IsT0.Should().BeTrue();
            var successTuple = completionResult.Value.AsT0;

            // 检查具体状态是 Error
            successTuple.blockStatus.Should().BeAssignableTo<OneOf<IdleBlockStatus, ErrorBlockStatus>>();
            successTuple.blockStatus.IsT1.Should().BeTrue(); // 确认是 ErrorBlockStatus
            var errorStatusFromTuple = successTuple.blockStatus.AsT1;
            errorStatusFromTuple.Should().BeOfType<ErrorBlockStatus>();

            // 检查失败结果
            successTuple.results.Should().NotBeNull();
            successTuple.results.Should().Contain(r => !r.Success && r.OriginalOperation == aiCommands[0]); // 包含失败的AI操作
            successTuple.results.Should()
                .Contain(r => r.Success && r.OriginalOperation == userCommands[0]); // 包含成功的用户操作


            // 检查 BlockManager 中的状态
            var finalStatus = await _manager.GetBlockAsync(_loadingBlockId);
            finalStatus.Should().BeOfType<ErrorBlockStatus>();

            // Assert: WorldState (wsPostAI/User 应该为 null)
            var errorStatus = (ErrorBlockStatus)finalStatus!;
            errorStatus.Block.wsPostAI.Should().BeNull();
            errorStatus.Block.wsPostUser.Should().BeNull();
            // 检查 Metadata 中是否记录了错误
            errorStatus.Block.Metadata.Should().ContainKey("Error");
            var errorsInMeta = errorStatus.Block.Metadata["Error"];
            errorsInMeta.Should().BeOfType<List<AtomicOperation>>(); // _FinalizeSuccessfulWorkflow 存的是失败的操作列表
            ((List<AtomicOperation>)errorsInMeta!).Should().ContainEquivalentOf(aiCommands[0]);
        }


        [Fact(DisplayName = "工作流完成_标记为失败_应进入Error")]
        public async Task HandleWorkflowCompletion_MarkedAsFailed_ShouldEnterErrorState()
        {
            // Arrange
            // 不需要特定命令，因为工作流直接失败

            // Act: 模拟工作流执行失败
            var completionResult = await _manager.HandleWorkflowCompletionAsync(
                _loadingBlockId,
                success: false, // 工作流直接失败
                rawText: "工作流执行失败",
                firstPartyCommands: [], // 无指令
                outputVariables: new Dictionary<string, object?>());

            // Assert: 状态转换
            completionResult.Should().NotBeNull();
            completionResult.Value.Should()
                .BeAssignableTo<OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult>
                    results), ConflictBlockStatus, ErrorBlockStatus>>();

            // 检查结果是错误分支 (直接返回 ErrorStatus)
            completionResult.Value.IsT2.Should().BeTrue();
            var errorStatus = completionResult.Value.AsT2;
            errorStatus.Should().BeOfType<ErrorBlockStatus>();


            // 检查 BlockManager 中的状态
            var finalStatus = await _manager.GetBlockAsync(_loadingBlockId);
            finalStatus.Should().BeOfType<ErrorBlockStatus>();

            // Assert: WorldState (wsPostAI/User 应该为 null)
            var actualErrorStatus = (ErrorBlockStatus)finalStatus!;
            actualErrorStatus.Block.wsPostAI.Should().BeNull();
            actualErrorStatus.Block.wsPostUser.Should().BeNull();
        }


        [Fact(DisplayName = "应用解决后的命令_应从Conflict进入Idle")]
        public async Task ApplyResolvedCommands_ShouldTransitionFromConflictToIdle()
        {
            // Arrange: 先将 Block 置于 Conflict 状态
            var userCmd = AtomicOperation.Modify(_targetEntityRef.Type, _targetEntityRef.Id, "健康", Operator.Sub, 5);
            var aiCmd = AtomicOperation.Modify(_targetEntityRef.Type, _targetEntityRef.Id, "健康", Operator.Add, 15);
            await _manager.EnqueueOrExecuteAtomicOperationsAsync(_loadingBlockId, [userCmd]);
            await _manager.HandleWorkflowCompletionAsync(_loadingBlockId, true, "冲突发生", [aiCmd],
                new Dictionary<string, object?>());
            var currentStatus = await _manager.GetBlockAsync(_loadingBlockId);
            currentStatus.Should().BeOfType<ConflictBlockStatus>(); // 确认进入了冲突状态

            // 定义用户解决冲突后的指令 (例如：取 AI 的结果)
            var resolvedCommands = new List<AtomicOperation>
            {
                AtomicOperation.Modify(_targetEntityRef.Type, _targetEntityRef.Id, "健康", Operator.Equal,
                    115) // 假设用户决定最终健康为 115 (100-5+15 的某种合并?)
                // 或者直接用 aiCmd:
                // aiCmd
            };


            // Act: 应用解决后的指令
            var resolveResult = await _manager.ApplyResolvedCommandsAsync(_loadingBlockId, resolvedCommands);


            // Assert: 状态转换
            resolveResult.blockStatus.Should().NotBeNull();
            resolveResult.blockStatus!.Value.IsT0.Should().BeTrue(); // Idle
            resolveResult.blockStatus!.Value.AsT0.Should().BeOfType<IdleBlockStatus>();
            resolveResult.results.Should().NotBeNull().And.OnlyContain(r => r.Success);


            // 检查 BlockManager 中的状态
            var finalStatus = await _manager.GetBlockAsync(_loadingBlockId);
            finalStatus.Should().BeOfType<IdleBlockStatus>();

            // Assert: WorldState 检查 (wsPostUser 应包含解决后的修改)
            var idleStatus = (IdleBlockStatus)finalStatus!;
            var finalWs = idleStatus.CurrentWorldState;
            var targetEntity = finalWs.FindEntity(_targetEntityRef);
            targetEntity.Should().NotBeNull();
            targetEntity!.GetAttribute("健康").Should().Be(115L); // 验证解决后的值
        }

        [Fact(DisplayName = "应用解决后的命令_解决命令失败_应从Conflict进入Error")]
        public async Task ApplyResolvedCommands_ResolvedCommandFails_ShouldTransitionFromConflictToError()
        {
            // Arrange: 先将 Block 置于 Conflict 状态 (同上一个测试)
            var userCmd = AtomicOperation.Modify(_targetEntityRef.Type, _targetEntityRef.Id, "健康", Operator.Sub, 5);
            var aiCmd = AtomicOperation.Modify(_targetEntityRef.Type, _targetEntityRef.Id, "健康", Operator.Add, 15);
            await _manager.EnqueueOrExecuteAtomicOperationsAsync(_loadingBlockId, [userCmd]);
            await _manager.HandleWorkflowCompletionAsync(_loadingBlockId, true, "冲突发生", [aiCmd],
                new Dictionary<string, object?>());
            var currentStatus = await _manager.GetBlockAsync(_loadingBlockId);
            currentStatus.Should().BeOfType<ConflictBlockStatus>();

            // 定义会失败的解决后指令
            var resolvedCommands = new List<AtomicOperation>
            {
                AtomicOperation.Modify(EntityType.Item, "non_existent_item_resolve", "属性", Operator.Equal,
                    "value") // 对不存在的实体操作
            };

            // Act: 应用解决后的指令
            var resolveResult = await _manager.ApplyResolvedCommandsAsync(_loadingBlockId, resolvedCommands);

            // Assert: 状态转换
            resolveResult.blockStatus.Should().NotBeNull();
            resolveResult.blockStatus!.Value.IsT1.Should().BeTrue(); // Error
            resolveResult.blockStatus!.Value.AsT1.Should().BeOfType<ErrorBlockStatus>();
            resolveResult.results.Should().NotBeNull().And.Contain(r => !r.Success); // 应该包含失败结果


            // 检查 BlockManager 中的状态
            var finalStatus = await _manager.GetBlockAsync(_loadingBlockId);
            finalStatus.Should().BeOfType<ErrorBlockStatus>();

            // Assert: WorldState (wsPostAI/User 应该为 null)
            var errorStatus = (ErrorBlockStatus)finalStatus!;
            errorStatus.Block.wsPostAI.Should().BeNull();
            errorStatus.Block.wsPostUser.Should().BeNull();
        }
    }
}