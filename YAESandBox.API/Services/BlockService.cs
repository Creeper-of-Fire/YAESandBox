using YAESandBox.API.Controllers;
using YAESandBox.API.DTOs;
using YAESandBox.Core.Action;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;
using OneOf;

namespace YAESandBox.API.Services;

// 定义 BlockServices 接口 (可选，但良好实践)
public interface IBlockServices
{
    Task<string> CreateRootBlockAsync(WorldState initialWorldState, GameState initialGameState);
    Task<BlockStatus?> GetBlockAsync(string blockId); // 获取原始 Block 对象
    Task<BlockSummaryDto?> GetBlockSummaryDtoAsync(string blockId);
    Task<IEnumerable<BlockSummaryDto>> GetAllBlockSummariesAsync();
    Task<BlockDetailDto?> GetBlockDetailDtoAsync(string blockId);
    Task<(UpdateResult result, string? message)> SelectChildBlockAsync(string blockId, int selectedChildIndex);
    Task<GameState?> GetBlockGameStateAsync(string blockId);
    Task<UpdateResult> UpdateBlockGameStateAsync(string blockId, Dictionary<string, object?> settingsToUpdate);
    Task<IEnumerable<BaseEntity>?> GetAllEntitiesSummaryAsync(string blockId); // 返回 Core 对象供 Controller 映射
    Task<BaseEntity?> GetEntityDetailAsync(string blockId, TypedID entityRef); // 返回 Core 对象供 Controller 映射
    Task<AtomicExecutionResult> EnqueueOrExecuteAtomicOperationsAsync(string blockId, List<AtomicOperation> operations);

    // --- Workflow Interaction Methods ---
    /// <summary>
    /// 为新的工作流创建一个子 Block。
    /// </summary>
    /// <param name="parentBlockId">父 Block ID。</param>
    /// <param name="triggerParams">触发工作流的参数。</param>
    /// <returns>新创建的子 Block ID，如果失败则返回 null。</returns>
    Task<string?> CreateChildBlockForWorkflowAsync(string parentBlockId, Dictionary<string, object?> triggerParams);

    /// <summary>
    /// 处理工作流执行完成后的回调。
    /// </summary>
    /// <param name="blockId">完成的工作流对应的 Block ID。</param>
    /// <param name="success">工作流是否成功执行。</param>
    /// <param name="rawText">工作流生成的原始文本内容。</param>
    /// <param name="firstPartyCommands">工作流生成的原子指令。</param>
    /// <param name="outputVariables">工作流输出的变量 (可选，用于元数据等)。</param>
    Task HandleWorkflowCompletionAsync(string blockId, bool success, string rawText,
        List<AtomicOperation> firstPartyCommands, Dictionary<string, object?> outputVariables);

    /// <summary>
    /// 应用用户解决冲突后提交的指令列表。
    /// </summary>
    /// <param name="blockId">需要应用指令的 Block ID。</param>
    /// <param name="resolvedCommands">解决冲突后的指令列表。</param>
    Task ApplyResolvedCommandsAsync(string blockId, List<AtomicOperation> resolvedCommands);

    // --- Internal Helper (Potentially needed by WorkflowService or internally) ---
    // Task<List<AtomicOperation>> GetAndClearPendingCommandsAsync(string blockId); // Made internal or removed if handled within HandleWorkflowCompletionAsync
    // Task<bool> SetBlockStatusAsync(string blockId, BlockStatusCode newStatusCode, ...); // Made internal or controlled via HandleWorkflowCompletionAsync/ApplyResolvedCommandsAsync

    // Kept for potential direct use or testing, though workflow usually clones
    Task<WorldState?> GetWsInputForNewBlockAsync(string parentBlockId);
    Task<GameState?> GetGsForNewBlockAsync(string parentBlockId);
}

public class BlockService(INotifierService notifierService, BlockManager blockManager) : IBlockServices
{
    private INotifierService notifierService { get; } = notifierService;
    private BlockManager blockManager { get; } = blockManager;

    private async Task CreateDefaultRootBlockIfNeededAsync()
    {
        if (this.blocks.IsEmpty)
        {
            var rootId = "root_default"; // Use a consistent default ID for simplicity
            if (!this.blocks.ContainsKey(rootId)) // Check if it somehow exists
            {
                Log.Warning($"未找到任何 Block，创建默认根 Block: {rootId}");
                var initialWs = new WorldState();
                var initialGs = new GameState();
                initialGs["FocusCharacter"] = "player_default";
                // Manually create and add the root block to bypass CreateRootBlockAsync's Guid generation
                var rootBlock = new Block(rootId, initialWorldState: initialWs, initialGameState: initialGs);
                rootBlock.Metadata["IsRoot"] = true;
                if (this.blocks.TryAdd(rootId, rootBlock))
                {
                    Log.Info($"默认根 Block '{rootId}' 已创建并添加。");
                    await this.notifierService.NotifyBlockStatusUpdateAsync(rootId, rootBlock.StatusCode);
                    // TODO: Persist
                }
                else
                {
                    Log.Error($"添加默认根 Block '{rootId}' 失败。");
                }
            }
        }
    }

    public async Task<string> CreateRootBlockAsync(WorldState initialWorldState, GameState initialGameState)
    {
        this.blockManager.CreateChildBlock_Async(null,)
    }

    public Task<BlockStatus?> GetBlockAsync(string blockId) =>
        this.blockManager.GetBlockAsync(blockId);

    public async Task<BlockSummaryDto?> GetBlockSummaryDtoAsync(string blockId)
    {
        var block = await this.GetBlockAsync(blockId);
        return block == null ? null : this.MapToSummaryDto(block);
    }

    public Task<IEnumerable<BlockSummaryDto>> GetAllBlockSummariesAsync()
    {
        // Order by CreationTime from Metadata if available
        var summaries = this.blocks.Values
            .Select(this.MapToSummaryDto)
            .OrderBy(b => b.CreationTime) // Assuming CreationTime is mapped
            .ToList();
        return Task.FromResult<IEnumerable<BlockSummaryDto>>(summaries);
    }

    public async Task<BlockDetailDto?> GetBlockDetailDtoAsync(string blockId)
    {
        var block = await this.GetBlockAsync(blockId);
        return block == null ? null : this.MapToDetailDto(block);
    }

    public async Task<(UpdateResult result, string? message)> SelectChildBlockAsync(string blockId,
        int selectedChildIndex)
    {
        // (Logic unchanged)
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
            {
                return (UpdateResult.NotFound, $"Block with ID '{blockId}' not found.");
            }

            if (selectedChildIndex >= 0 && selectedChildIndex < block.ChildrenInfo.Count)
            {
                block.SelectedChildIndex = selectedChildIndex;
                Log.Debug($"Block '{blockId}': Selected child index set to {selectedChildIndex}.");
                // TODO: 持久化 Block 的变更
                return (UpdateResult.Success, null);
            }
            else
            {
                string validRange = block.ChildrenInfo.Count == 0
                    ? "N/A (no children)"
                    : $"0 to {block.ChildrenInfo.Count - 1}";
                string msg = $"Invalid child index '{selectedChildIndex}'. Valid range: {validRange}.";
                Log.Warning($"Block '{blockId}': {msg}");
                return (UpdateResult.InvalidOperation, msg);
            }
        }
    }

    public async Task<GameState?> GetBlockGameStateAsync(string blockId)
    {
        var block = await this.GetBlockAsync(blockId);
        return block?.gameState;
    }

    public async Task<UpdateResult> UpdateBlockGameStateAsync(string blockId,
        Dictionary<string, object?> settingsToUpdate)
    {
        // (Logic mostly unchanged, check statusCode carefully)
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
            {
                return UpdateResult.NotFound;
            }

            // Allow gameState changes unless actively loading or resolving (could be debated)
            if (block.Status == BlockStatusCode.Loading /*|| block.StatusCode == BlockStatusCode.ResolvingConflict */
               ) // Allow during conflict? Maybe.
            {
                Log.Warning($"尝试在 Block '{blockId}' 处于 '{block.Status}' 状态时修改 gameState，已拒绝。");
                return UpdateResult.Conflict;
            }

            foreach (var kvp in settingsToUpdate)
            {
                block.GameState[kvp.Key] = kvp.Value;
            }

            Log.Debug($"Block '{blockId}': gameState 已更新。");
            // TODO: 持久化 Block
            return UpdateResult.Success;
        }
    }

    public async Task<IEnumerable<BaseEntity>?> GetAllEntitiesSummaryAsync(string blockId)
    {
        // Uses GetTargetWorldStateForInteraction, which handles statusCode checks
        var block = await this.GetBlockAsync(blockId);
        if (block == null) return null;

        try
        {
            var targetWs = block.GetTargetWorldStateForInteraction();
            // Return all non-destroyed entities
            return targetWs.Items.Values.Cast<BaseEntity>()
                .Concat(targetWs.Characters.Values)
                .Concat(targetWs.Places.Values)
                .Where(e => !e.IsDestroyed)
                .ToList(); // Create a copy
        }
        catch (InvalidOperationException ex) // Catch specific exception from GetTargetWorldState...
        {
            Log.Warning(
                $"Block '{blockId}': Cannot get entities summary in current state ({block.StatusCode}): {ex.Message}");
            return null; // Or return empty list?
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Block '{blockId}': 获取实体摘要时出错: {ex.Message}");
            return null;
        }
    }

    public async Task<BaseEntity?> GetEntityDetailAsync(string blockId, TypedID entityRef)
    {
        // Uses GetTargetWorldStateForInteraction
        var block = await this.GetBlockAsync(blockId);
        if (block == null) return null;

        try
        {
            var targetWs = block.GetTargetWorldStateForInteraction();
            return targetWs.FindEntity(entityRef, includeDestroyed: false); // Find non-destroyed
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(
                $"Block '{blockId}': Cannot get entity detail in current state ({block.StatusCode}): {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Block '{blockId}': 获取实体 '{entityRef}' 详情时出错: {ex.Message}");
            return null;
        }
    }

    public async Task<AtomicExecutionResult> EnqueueOrExecuteAtomicOperationsAsync(string blockId,
        List<AtomicOperation> operations)
    {
        var (blockStatus, results) = await this.blockManager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);
        if (blockStatus == null)
            return AtomicExecutionResult.NotFound;

        var atomicExecutionResult = blockStatus.Value.Match(
            idle => AtomicExecutionResult.Executed,
            loading => AtomicExecutionResult.Queued,
            conflict => AtomicExecutionResult.ConflictState,
            error => AtomicExecutionResult.Error);

        if (results == null)
            return atomicExecutionResult;
        await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, blockStatus.Value.ForceResult
            <IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus, BlockStatus, BlockStatusCode>(
                target => target.StatusCode));
        if (results.Count > 0)
        {
            await this.notifierService.NotifyStateUpdateAsync(blockId,
                results.Select(x => x.OriginalOperation.EntityId));
        }

        return atomicExecutionResult;
    }


    // --- Workflow Interaction Methods ---

    public async Task<string?> CreateChildBlockForWorkflowAsync(string parentBlockId,
        Dictionary<string, object?> triggerParams)
    {
        var newBlock = await this.blockManager.CreateChildBlock_Async(parentBlockId, triggerParams);

        if (newBlock == null)
            return null;
        // Notify about the new block and parent update
        await this.notifierService.NotifyBlockStatusUpdateAsync(newBlock.block.BlockId, newBlock.StatusCode);
        // Maybe notify about parent's selection change? Or batch updates.

        return newBlock.block.BlockId;
    }

    public async Task HandleWorkflowCompletionAsync(string blockId, bool success, string rawText,
        List<AtomicOperation> firstPartyCommands, Dictionary<string, object?> outputVariables)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
            {
                Log.Error($"处理工作流完成失败: Block '{blockId}' 未找到。");
                return;
            }

            if (block.Status != BlockStatusCode.Loading)
            {
                Log.Warning($"收到 Block '{blockId}' 的工作流完成回调，但其状态为 {block.Status} (非 Loading)。可能重复或过时。");
                // Decide whether to proceed or ignore. Let's ignore for now.
                return;
            }

            // Store output variables in metadata?
            block.Metadata["WorkflowOutputVariables"] = outputVariables;


            if (success)
            {
                Log.Info($"Block '{blockId}': 工作流成功完成。准备处理指令和状态。");
                // 1. Get pending user commands accumulated during loading
                var pendingUserCommands = new List<AtomicOperation>(block.PendingUserCommands);
                block.PendingUserCommands.Clear();
                Log.Debug($"Block '{blockId}': 获取了 {pendingUserCommands.Count} 条暂存的用户指令。");

                // 2. Conflict Resolution (Placeholder: just combine, user commands after AI)
                var resolvedCommands = new List<AtomicOperation>(firstPartyCommands);
                resolvedCommands.AddRange(pendingUserCommands); // Simple append strategy
                Log.Debug(
                    $"Block '{blockId}': 合并了 {firstPartyCommands.Count} 条 AI 指令和 {pendingUserCommands.Count} 条用户指令，共 {resolvedCommands.Count} 条。");
                // TODO: Implement real conflict detection and resolution strategy.
                // If conflicts detected -> block.StatusCode = ResolvingConflict; notify; return;

                // 3. Apply resolved commands to WsPostAI (based on WsInput)
                try
                {
                    if (block.WsInput == null)
                    {
                        Log.Error($"Block '{blockId}': WsInput is null, cannot generate WsPostAI.");
                        await this.SetBlockStatusInternalAsync(blockId, BlockStatusCode.Error, rawText,
                            "Internal Error: WsInput missing.");
                        return;
                    }

                    block.WsPostAI = block.WsInput.Clone(); // Start from input state
                    var changedAiIds = this.ExecuteAtomicOperations(block.WsPostAI, resolvedCommands);

                    // *** ADD DETAILED LOGGING HERE ***
                    Log.Debug($"Block '{blockId}': Post ExecuteAtomicOperations on WsPostAI.");
                    Log.Debug(
                        $"  WsPostAI - Items: {block.WsPostAI.Items.Count}, Characters: {block.WsPostAI.Characters.Count}, Places: {block.WsPostAI.Places.Count}");
                    var knight = block.WsPostAI.FindEntityById("clumsy-knight", EntityType.Character);
                    Log.Debug($"  WsPostAI - Found clumsy-knight? {(knight != null ? "Yes" : "No")}");
                    var entrance = block.WsPostAI.FindEntityById("castle-entrance", EntityType.Place);
                    Log.Debug($"  WsPostAI - Found castle-entrance? {(entrance != null ? "Yes" : "No")}");
                    if (entrance != null)
                    {
                        Log.Debug(
                            $"  WsPostAI - castle-entrance description: '{entrance.GetAttribute("description")?.ToString() ?? "N/A"}'");
                    }
                    // *** END OF ADDED LOGGING ***

                    Log.Debug($"Block '{blockId}': 已解决的指令已应用到 WsPostAI，影响 {changedAiIds.Count} 个实体。");

                    // 4. Create WsPostUser based on WsPostAI
                    block.WsPostUser = block.WsPostAI.Clone();
                    Log.Debug($"Block '{blockId}': 已基于 WsPostAI 创建 WsPostUser。");

                    // 5. Update Block Content and finalize state
                    block.BlockContent = rawText;
                    block.WsTemp = null; // Discard temporary state
                    block.Status = BlockStatusCode.Idle; // Set to Idle
                    Log.Info($"Block '{blockId}': 状态设置为 Idle。");

                    // TODO: Persist Block (StatusCode, Content, WsPostAI, WsPostUser)

                    // 6. Notify statusCode and state update
                    await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, BlockStatusCode.Idle);
                    if (changedAiIds.Count > 0) // Notify based on changes applied to WsPostAI/WsPostUser
                    {
                        await this.notifierService.NotifyStateUpdateAsync(blockId, changedAiIds);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Block '{blockId}': 应用已解决指令时出错: {ex.Message}");
                    await this.SetBlockStatusInternalAsync(blockId, BlockStatusCode.Error, rawText,
                        $"Error applying commands: {ex.Message}");
                }
            }
            else // Workflow failed
            {
                Log.Error($"Block '{blockId}': 工作流执行失败。");
                await this.SetBlockStatusInternalAsync(blockId, BlockStatusCode.Error, rawText,
                    "Workflow execution failed.");
            }
        }
    }


    public async Task ApplyResolvedCommandsAsync(string blockId, List<AtomicOperation> resolvedCommands)
    {
        var (blockStatus, results) = await this.blockManager.ApplyResolvedCommandsAsync(blockId, resolvedCommands);
        if (blockStatus == null || results == null)
            return;

        // 使用 Match 并返回一个布尔值决定是否继续
        if (!blockStatus.Value.Match(idle => true, error =>
            {
                Log.Warning($"未能应用已解决的命令，Block '{blockId}' 处于错误状态。");
                return false;
            }))
            return;

        await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, blockStatus.Value.AsT0.StatusCode);
        if (results.Count > 0)
        {
            await this.notifierService.NotifyStateUpdateAsync(blockId,
                results.Select(x => x.OriginalOperation.EntityId));
        }
    }

    // Helper to set error statusCode internally
    private async Task SetBlockStatusInternalAsync(string blockId, BlockStatusCode statusCode, string? content = null,
        string? errorMessage = null)
    {
        // No lock here, assumes called from within a locked context
        if (this.blocks.TryGetValue(blockId, out var block))
        {
            block.Status = statusCode;
            if (content != null) block.BlockContent = content;
            if (errorMessage != null) block.Metadata["Error"] = errorMessage;
            block.WsTemp = null; // Clear temp state on error
            block.PendingUserCommands.Clear(); // Clear pending commands on error? Or keep them? Clear for now.

            Log.Info($"Block '{blockId}': 内部状态设置为 {statusCode}。");
            // TODO: Persist Block

            // Notify statusCode update
            await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, statusCode);
        }
    }

    // --- DTO Mapping Helpers ---
    private BlockSummaryDto MapToSummaryDto(Block block)
    {
        return new BlockSummaryDto
        {
            BlockId = block.BlockId,
            ParentBlockId = block.ParentBlockId,
            StatusCode = block.StatusCode,
            SelectedChildIndex = block.SelectedChildIndex,
            ContentSummary = this.GetContentSummary(block.BlockContent),
            // Safely access CreationTime from metadata
            CreationTime = block.Metadata.TryGetValue("CreationTime", out var timeObj) && timeObj is DateTime time
                ? time
                : DateTime.MinValue
        };
    }

    private BlockDetailDto MapToDetailDto(BlockStatus block)
    {
        return new BlockDetailDto
        {
            BlockId = block.block.BlockId,
            ParentBlockId = block.block.ParentBlockId,
            StatusCode = block.StatusCode,
            SelectedChildIndex = block.block.SelectedChildIndex,
            ContentSummary = this.GetContentSummary(block.block.BlockContent),
            CreationTime = block.block.Metadata.TryGetValue("CreationTime", out var timeObj) && timeObj is DateTime time
                ? time
                : DateTime.MinValue,
            BlockContent = block.block.BlockContent,
            Metadata = new Dictionary<string, object?>(block.block.Metadata), // Return copy
            ChildrenInfo = new Dictionary<int, string>(block.block.ChildrenInfo) // Return copy
        };
    }
}