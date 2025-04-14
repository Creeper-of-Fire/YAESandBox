// --- START OF FILE BlockManager.cs ---

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nito.AsyncEx;
using YAESandBox.API.Controllers; // For AsyncLock, UpdateResult
using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.API.DTOs; // For DTO mapping if done here
using YAESandBox.Depend; // For Log

namespace YAESandBox.API.Services;

// 定义 BlockManager 接口 (可选，但良好实践)
public interface IBlockManager
{
    Task<string> CreateRootBlockAsync(WorldState initialWorldState, GameState initialGameState);
    Task<Block?> GetBlockAsync(string blockId); // 获取原始 Block 对象
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
    // Task<bool> SetBlockStatusAsync(string blockId, BlockStatus newStatus, ...); // Made internal or controlled via HandleWorkflowCompletionAsync/ApplyResolvedCommandsAsync

    // Kept for potential direct use or testing, though workflow usually clones
    Task<WorldState?> GetWsInputForNewBlockAsync(string parentBlockId);
    Task<GameState?> GetGsForNewBlockAsync(string parentBlockId);
}

public class BlockManager : IBlockManager
{
    private ConcurrentDictionary<string, Block> blocks { get; } = new();
    private ConcurrentDictionary<string, AsyncLock> blockLocks { get; } = new();
    private INotifierService notifierService { get; }

    public BlockManager(INotifierService notifierService)
    {
        this.notifierService = notifierService;
        // ... (初始化和加载逻辑不变) ...
        if (this.blocks.IsEmpty)
        {
            Log.Info("BlockManager 初始化：没有从持久化存储加载任何 Block，将创建默认根 Block。");
            CreateDefaultRootBlockIfNeededAsync().GetAwaiter().GetResult();
        }
        else
        {
            Log.Info($"BlockManager 初始化：从持久化存储加载了 {this.blocks.Count} 个 Block。");
        }
    }

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
                    await this.notifierService.NotifyBlockStatusUpdateAsync(rootId, rootBlock.Status);
                    // TODO: Persist
                }
                else
                {
                    Log.Error($"添加默认根 Block '{rootId}' 失败。");
                }
            }
        }
    }


    private AsyncLock GetLockForBlock(string blockId)
    {
        return this.blockLocks.GetOrAdd(blockId, _ => new AsyncLock());
    }

    // --- Standard Methods (mostly unchanged, review GetTargetWorldStateForInteraction) ---

    public async Task<string> CreateRootBlockAsync(WorldState initialWorldState, GameState initialGameState)
    {
        var blockId = $"root_{Guid.NewGuid().ToString("N")[..8]}";
        var rootBlock = new Block(blockId, initialWorldState, initialGameState);
        rootBlock.Metadata["IsRoot"] = true;

        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (this.blocks.TryAdd(blockId, rootBlock))
            {
                Log.Info($"根 Block '{blockId}' 已创建并添加。");
                await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, rootBlock.Status);
                // TODO: 持久化存储新创建的 Block
                return blockId;
            }
            else
            {
                Log.Error($"尝试创建根 Block '{blockId}' 失败，可能已存在同名 Block。");
                throw new InvalidOperationException($"Failed to add root block '{blockId}'. It might already exist.");
            }
        }
    }

    public Task<Block?> GetBlockAsync(string blockId)
    {
        Log.Debug($"GetBlockAsync: 尝试获取 Block ID: '{blockId}'"); // <-- 添加日志
        this.blocks.TryGetValue(blockId, out var block);
        if (block == null)
        {
            Log.Warning($"GetBlockAsync: 未在 _blocks 字典中找到 Block ID: '{blockId}'。当前字典大小: {this.blocks.Count}"); // <-- 添加日志
            // 可以选择性地打印出所有 keys 来调试
            // var keys = string.Join(", ", _blocks.Keys);
            // Log.Debug($"GetBlockAsync: 当前所有 Block IDs: [{keys}]");
        }
        else
        {
            Log.Debug($"GetBlockAsync: 成功找到 Block ID: '{blockId}'，状态为: {block.Status}"); // <-- 添加日志
        }
        return Task.FromResult(block);
    }

    public async Task<BlockSummaryDto?> GetBlockSummaryDtoAsync(string blockId)
    {
        var block = await GetBlockAsync(blockId);
        return block == null ? null : MapToSummaryDto(block);
    }

    public Task<IEnumerable<BlockSummaryDto>> GetAllBlockSummariesAsync()
    {
        // Order by CreationTime from Metadata if available
        var summaries = this.blocks.Values
            .Select(MapToSummaryDto)
            .OrderBy(b => b.CreationTime) // Assuming CreationTime is mapped
            .ToList();
        return Task.FromResult<IEnumerable<BlockSummaryDto>>(summaries);
    }

    public async Task<BlockDetailDto?> GetBlockDetailDtoAsync(string blockId)
    {
        var block = await GetBlockAsync(blockId);
        return block == null ? null : MapToDetailDto(block);
    }

    public async Task<(UpdateResult result, string? message)> SelectChildBlockAsync(string blockId,
        int selectedChildIndex)
    {
        // (Logic unchanged)
        using (await GetLockForBlock(blockId).LockAsync())
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
        var block = await GetBlockAsync(blockId);
        return block?.GameState;
    }

    public async Task<UpdateResult> UpdateBlockGameStateAsync(string blockId,
        Dictionary<string, object?> settingsToUpdate)
    {
        // (Logic mostly unchanged, check status carefully)
        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
            {
                return UpdateResult.NotFound;
            }

            // Allow GameState changes unless actively loading or resolving (could be debated)
            if (block.Status == BlockStatus.Loading /*|| block.Status == BlockStatus.ResolvingConflict */
               ) // Allow during conflict? Maybe.
            {
                Log.Warning($"尝试在 Block '{blockId}' 处于 '{block.Status}' 状态时修改 GameState，已拒绝。");
                return UpdateResult.Conflict;
            }

            foreach (var kvp in settingsToUpdate)
            {
                block.GameState[kvp.Key] = kvp.Value;
            }

            Log.Debug($"Block '{blockId}': GameState 已更新。");
            // TODO: 持久化 Block
            return UpdateResult.Success;
        }
    }

    public async Task<IEnumerable<BaseEntity>?> GetAllEntitiesSummaryAsync(string blockId)
    {
        // Uses GetTargetWorldStateForInteraction, which handles status checks
        var block = await GetBlockAsync(blockId);
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
                $"Block '{blockId}': Cannot get entities summary in current state ({block.Status}): {ex.Message}");
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
        var block = await GetBlockAsync(blockId);
        if (block == null) return null;

        try
        {
            var targetWs = block.GetTargetWorldStateForInteraction();
            return targetWs.FindEntity(entityRef, includeDestroyed: false); // Find non-destroyed
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning($"Block '{blockId}': Cannot get entity detail in current state ({block.Status}): {ex.Message}");
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
        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
            {
                return AtomicExecutionResult.NotFound;
            }

            switch (block.Status)
            {
                case BlockStatus.Idle:
                case BlockStatus.Error: // Allow modifications in Error state
                    try
                    {
                        // Get WsPostUser (or WsInput as fallback)
                        var targetWs = block.WsPostUser ?? block.WsInput;
                        if (targetWs == null)
                        {
                            Log.Error(
                                $"Block '{blockId}' in state {block.Status} has no WsPostUser or WsInput to modify.");
                            return AtomicExecutionResult.Error; // Cannot proceed
                        }

                        // Ensure WsPostUser exists if we modify based on WsInput
                        if (block.WsPostUser == null)
                        {
                            block.WsPostUser = targetWs.Clone();
                            targetWs = block.WsPostUser; // Modify the new WsPostUser
                            Log.Debug($"Block '{blockId}': Cloned WsInput to create WsPostUser for modification.");
                        }


                        var changedEntityIds = ExecuteAtomicOperations(targetWs, operations);
                        Log.Debug(
                            $"Block '{blockId}' ({block.Status}): 原子操作已执行到 WsPostUser，影响 {changedEntityIds.Count} 个实体。");
                        // TODO: 持久化 Block (WsPostUser changed)

                        if (changedEntityIds.Count > 0)
                        {
                            await this.notifierService.NotifyStateUpdateAsync(blockId, changedEntityIds);
                        }

                        return AtomicExecutionResult.Executed;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Block '{blockId}': 执行原子操作时出错: {ex.Message}");
                        // Consider setting status to Error?
                        // await SetBlockStatusInternalAsync(blockId, BlockStatus.Error); // If SetBlockStatusInternalAsync exists
                        return AtomicExecutionResult.Error;
                    }

                case BlockStatus.Loading:
                    // 1. Queue the command
                    block.PendingUserCommands.AddRange(operations);
                    Log.Debug(
                        $"Block '{blockId}' ({block.Status}): 原子操作已暂存 ({operations.Count} 条)。当前暂存总数: {block.PendingUserCommands.Count}");

                    // 2. Apply to WsTemp for immediate feedback (best effort)
                    if (block.WsTemp != null)
                    {
                        try
                        {
                            var changedTempIds = ExecuteAtomicOperations(block.WsTemp, operations);
                            Log.Debug(
                                $"Block '{blockId}' ({block.Status}): 原子操作已尝试应用到 WsTemp，影响 {changedTempIds.Count} 个实体 (仅用于显示)。");
                            // TODO: Persist pending commands? Maybe not.

                            // Notify that the *temporary* state changed
                            if (changedTempIds.Count > 0)
                            {
                                // Send a specific signal? Or just the standard one? Standard might be confusing.
                                // Let's stick to the standard one for now, front-end needs to know it's temporary.
                                await this.notifierService.NotifyStateUpdateAsync(blockId, changedTempIds);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Block '{blockId}': 应用原子操作到 WsTemp 时出错 (已忽略): {ex.Message}");
                        }
                    }
                    else
                    {
                        Log.Warning(
                            $"Block '{blockId}' ({block.Status}): WsTemp is null, cannot apply queued operations for immediate feedback.");
                    }

                    return AtomicExecutionResult.Queued;

                case BlockStatus.ResolvingConflict:
                    // Queue commands during conflict resolution as well
                    block.PendingUserCommands.AddRange(operations);
                    Log.Debug(
                        $"Block '{blockId}' ({block.Status}): 原子操作已暂存 ({operations.Count} 条), Block 正在解决冲突。当前暂存总数: {block.PendingUserCommands.Count}");
                    // Do NOT apply to WsTemp here, as the base state is uncertain until resolved.
                    return AtomicExecutionResult.Queued; // Or return ConflictState? Queued seems more accurate.

                default:
                    Log.Error($"Block '{blockId}': 处于未知的状态 '{block.Status}'，无法处理原子操作。");
                    return AtomicExecutionResult.Error;
            }
        }
    }


    // --- Workflow Interaction Methods ---

    public async Task<string?> CreateChildBlockForWorkflowAsync(string parentBlockId,
        Dictionary<string, object?> triggerParams)
    {
        string newBlockId = $"blk_{Guid.NewGuid().ToString("N")[..8]}"; // Generate ID

        using (await GetLockForBlock(parentBlockId).LockAsync()) // Lock parent to add child info
        {
            // 1. Get Parent Block
            if (!this.blocks.TryGetValue(parentBlockId, out var parentBlock))
            {
                Log.Error($"Cannot create child block: Parent block '{parentBlockId}' not found.");
                return null;
            }

            // 2. Check Parent Status (cannot create child if parent is loading/error?)
            if (parentBlock.Status == BlockStatus.Loading || parentBlock.Status == BlockStatus.Error ||
                parentBlock.Status == BlockStatus.ResolvingConflict)
            {
                Log.Error(
                    $"Cannot create child block: Parent block '{parentBlockId}' is in status {parentBlock.Status}.");
                return null;
            }


            // 3. Ensure parent has a final state (WsPostUser)
            if (parentBlock.WsPostUser == null)
            {
                Log.Error($"Cannot create child block: Parent block '{parentBlockId}' has no WsPostUser state.");
                // Attempt recovery? Or just fail. Let's fail for now.
                // Maybe clone WsInput if WsPostUser is null?
                if (parentBlock.WsInput == null)
                {
                    Log.Error($"Cannot create child block: Parent block '{parentBlockId}' also has no WsInput state.");
                    return null;
                }

                Log.Warning(
                    $"Parent block '{parentBlockId}' WsPostUser is null, using WsInput as base for child '{newBlockId}'.");
                parentBlock.WsPostUser = parentBlock.WsInput.Clone(); // Create it if missing
            }

            // Clone parent's state for the new block's input
            WorldState wsInputClone = parentBlock.WsPostUser.Clone();
            GameState gsClone = parentBlock.GameState.Clone();


            // 4. Create the new Block instance (use internal constructor or dedicated method)
            var newBlock =
                new Block(newBlockId, parentBlockId, wsInputClone, gsClone, triggerParams); // Use constructor overload

            // 5. Add Child Info to Parent
            int childIndex = parentBlock.ChildrenInfo.Count;
            parentBlock.ChildrenInfo[childIndex] = newBlockId;
            parentBlock.TriggeredChildParams[newBlockId] =
                triggerParams ?? new Dictionary<string, object?>(); // Store params associated with the child ID
            parentBlock.SelectedChildIndex = childIndex; // Select the new child
            Log.Debug($"父 Block '{parentBlockId}': 已添加子 Block '{newBlockId}' 记录，索引为 {childIndex}。");
            // TODO: Persist Parent Block changes


            // 6. Add New Block to Manager's Dictionary (Lock the *new* block ID)
            using (await GetLockForBlock(newBlockId).LockAsync())
            {
                if (this.blocks.TryAdd(newBlockId, newBlock))
                {
                    Log.Info($"新 Block '{newBlockId}' 已创建并添加，状态: {newBlock.Status}。");
                    // TODO: Persist New Block
                }
                else
                {
                    Log.Error($"添加新 Block '{newBlockId}' 失败，可能已存在同名 Block。");
                    // Clean up parent's child info? Complex rollback needed. For now, log error.
                    parentBlock.ChildrenInfo.Remove(childIndex); // Attempt cleanup
                    parentBlock.TriggeredChildParams.Remove(newBlockId);
                    // Reset selection? Maybe select last child or -1
                    parentBlock.SelectedChildIndex =
                        parentBlock.ChildrenInfo.Count > 0 ? parentBlock.ChildrenInfo.Keys.Max() : -1;

                    return null; // Indicate failure
                }
            }

            // 7. Notify about the new block and parent update
            await this.notifierService.NotifyBlockStatusUpdateAsync(newBlockId, newBlock.Status);
            // Maybe notify about parent's selection change? Or batch updates.

            return newBlockId;
        }
    }

    public async Task HandleWorkflowCompletionAsync(string blockId, bool success, string rawText,
        List<AtomicOperation> firstPartyCommands, Dictionary<string, object?> outputVariables)
    {
        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
            {
                Log.Error($"处理工作流完成失败: Block '{blockId}' 未找到。");
                return;
            }

            if (block.Status != BlockStatus.Loading)
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
                // If conflicts detected -> block.Status = ResolvingConflict; notify; return;

                // 3. Apply resolved commands to WsPostAI (based on WsInput)
                try
                {
                    if (block.WsInput == null)
                    {
                        Log.Error($"Block '{blockId}': WsInput is null, cannot generate WsPostAI.");
                        await SetBlockStatusInternalAsync(blockId, BlockStatus.Error, rawText,
                            "Internal Error: WsInput missing.");
                        return;
                    }

                    block.WsPostAI = block.WsInput.Clone(); // Start from input state
                    var changedAiIds = ExecuteAtomicOperations(block.WsPostAI, resolvedCommands);
                    
                    // *** ADD DETAILED LOGGING HERE ***
                    Log.Debug($"Block '{blockId}': Post ExecuteAtomicOperations on WsPostAI.");
                    Log.Debug($"  WsPostAI - Items: {block.WsPostAI.Items.Count}, Characters: {block.WsPostAI.Characters.Count}, Places: {block.WsPostAI.Places.Count}");
                    var knight = block.WsPostAI.FindEntityById("clumsy-knight", EntityType.Character);
                    Log.Debug($"  WsPostAI - Found clumsy-knight? {(knight != null ? "Yes" : "No")}");
                    var entrance = block.WsPostAI.FindEntityById("castle-entrance", EntityType.Place);
                    Log.Debug($"  WsPostAI - Found castle-entrance? {(entrance != null ? "Yes" : "No")}");
                    if (entrance != null)
                    {
                        Log.Debug($"  WsPostAI - castle-entrance description: '{entrance.GetAttribute("description")?.ToString() ?? "N/A"}'");
                    }
                    // *** END OF ADDED LOGGING ***
                    
                    Log.Debug($"Block '{blockId}': 已解决的指令已应用到 WsPostAI，影响 {changedAiIds.Count} 个实体。");

                    // 4. Create WsPostUser based on WsPostAI
                    block.WsPostUser = block.WsPostAI.Clone();
                    Log.Debug($"Block '{blockId}': 已基于 WsPostAI 创建 WsPostUser。");

                    // 5. Update Block Content and finalize state
                    block.BlockContent = rawText;
                    block.WsTemp = null; // Discard temporary state
                    block.Status = BlockStatus.Idle; // Set to Idle
                    Log.Info($"Block '{blockId}': 状态设置为 Idle。");

                    // TODO: Persist Block (Status, Content, WsPostAI, WsPostUser)

                    // 6. Notify status and state update
                    await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, BlockStatus.Idle);
                    if (changedAiIds.Count > 0) // Notify based on changes applied to WsPostAI/WsPostUser
                    {
                        await this.notifierService.NotifyStateUpdateAsync(blockId, changedAiIds);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Block '{blockId}': 应用已解决指令时出错: {ex.Message}");
                    await SetBlockStatusInternalAsync(blockId, BlockStatus.Error, rawText,
                        $"Error applying commands: {ex.Message}");
                }
            }
            else // Workflow failed
            {
                Log.Error($"Block '{blockId}': 工作流执行失败。");
                await SetBlockStatusInternalAsync(blockId, BlockStatus.Error, rawText, "Workflow execution failed.");
            }
        }
    }


    public async Task ApplyResolvedCommandsAsync(string blockId, List<AtomicOperation> resolvedCommands)
    {
        // This logic is now mostly inside HandleWorkflowCompletionAsync after conflict resolution.
        // This method might be used if we implement manual conflict resolution flow.
        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
            {
                Log.Error($"尝试应用已解决指令失败: Block '{blockId}' 未找到。");
                return;
            }

            if (block.Status != BlockStatus.ResolvingConflict)
            {
                Log.Warning($"尝试应用已解决指令，但 Block '{blockId}' 状态为 {block.Status} (非 ResolvingConflict)。已忽略。");
                return;
            }

            Log.Info($"Block '{blockId}': 正在应用手动解决的冲突指令 ({resolvedCommands.Count} 条)。");

            try
            {
                // Apply to WsPostAI (assuming it exists from the initial workflow run)
                var baseWs = block.WsPostAI ?? block.WsInput; // Should ideally be WsPostAI
                if (baseWs == null)
                {
                    Log.Error($"Block '{blockId}': WsPostAI or WsInput is null, cannot apply resolved commands.");
                    await SetBlockStatusInternalAsync(blockId, BlockStatus.Error, block.BlockContent,
                        "Internal Error: Base state missing for conflict resolution.");
                    return;
                }

                // We modify WsPostAI in place based on user resolution
                block.WsPostAI =
                    baseWs.Clone(); // Clone before modify? Or modify WsPostAI directly? Let's modify directly for now.
                var changedIds = ExecuteAtomicOperations(block.WsPostAI, resolvedCommands); // Apply to WsPostAI

                // Re-create WsPostUser from the newly modified WsPostAI
                block.WsPostUser = block.WsPostAI.Clone();
                Log.Debug($"Block '{blockId}': 已基于修改后的 WsPostAI 更新 WsPostUser。");


                block.WsTemp = null; // Clear temp state
                block.Status = BlockStatus.Idle; // Resolution complete
                Log.Info($"Block '{blockId}': 冲突解决完成，状态设置为 Idle。");

                // TODO: Persist Block (Status, WsPostAI, WsPostUser)

                // Notify
                await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, BlockStatus.Idle);
                if (changedIds.Count > 0)
                {
                    await this.notifierService.NotifyStateUpdateAsync(blockId, changedIds);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Block '{blockId}': 应用手动解决的指令时出错: {ex.Message}");
                await SetBlockStatusInternalAsync(blockId, BlockStatus.Error, block.BlockContent,
                    $"Error applying resolved commands: {ex.Message}");
            }
        }
    }

    // Helper to set error status internally
    private async Task SetBlockStatusInternalAsync(string blockId, BlockStatus status, string? content = null,
        string? errorMessage = null)
    {
        // No lock here, assumes called from within a locked context
        if (this.blocks.TryGetValue(blockId, out var block))
        {
            block.Status = status;
            if (content != null) block.BlockContent = content;
            if (errorMessage != null) block.Metadata["Error"] = errorMessage;
            block.WsTemp = null; // Clear temp state on error
            block.PendingUserCommands.Clear(); // Clear pending commands on error? Or keep them? Clear for now.

            Log.Info($"Block '{blockId}': 内部状态设置为 {status}。");
            // TODO: Persist Block

            // Notify status update
            await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, status);
        }
    }


    // --- Other existing methods ---
    public async Task<WorldState?> GetWsInputForNewBlockAsync(string parentBlockId)
    {
        var parentBlock = await GetBlockAsync(parentBlockId);
        // Return clone of WsPostUser or WsInput as fallback
        var baseWs = parentBlock?.WsPostUser ?? parentBlock?.WsInput;
        if (baseWs == null)
        {
            Log.Error($"父 Block '{parentBlockId}' 不存在或没有 WsPostUser/WsInput，无法为新子 Block 提供输入 WorldState。");
            return null;
        }

        return baseWs.Clone();
    }

    public async Task<GameState?> GetGsForNewBlockAsync(string parentBlockId)
    {
        var parentBlock = await GetBlockAsync(parentBlockId);
        if (parentBlock?.GameState == null)
        {
            Log.Error($"父 Block '{parentBlockId}' 不存在或其 GameState 为 null，无法为新子 Block 提供输入 GameState。");
            return null;
        }

        return parentBlock.GameState.Clone();
    }

    private List<string> ExecuteAtomicOperations(WorldState worldState, IEnumerable<AtomicOperation> operations)
    {
        // (Logic unchanged)
        var changedEntityIds = new HashSet<string>();

        foreach (var op in operations)
        {
            try
            {
                BaseEntity? entity;
                switch (op.OperationType)
                {
                    case AtomicOperationType.CreateEntity:
                        entity = worldState.FindEntityById(op.EntityId, op.EntityType, includeDestroyed: true);
                        if (entity != null && !entity.IsDestroyed)
                        {
                            Log.Warning($"原子操作 Create: 实体 '{op.EntityType}:{op.EntityId}' 已存在且未销毁，将被覆盖。");
                        }

                        entity = op.EntityType switch
                        {
                            EntityType.Item => new Item(op.EntityId),
                            EntityType.Character => new Character(op.EntityId),
                            EntityType.Place => new Place(op.EntityId),
                            _ => throw new ArgumentOutOfRangeException($"不支持创建类型: {op.EntityType}")
                        };
                        if (op.InitialAttributes != null)
                        {
                            foreach (var attr in op.InitialAttributes)
                            {
                                entity.SetAttribute(attr.Key, attr.Value);
                            }
                        }

                        worldState.AddEntity(entity);
                        Log.Debug($"原子操作 Create: 成功创建/覆盖实体 '{entity.TypedId}'。");
                        changedEntityIds.Add(entity.EntityId);
                        break;

                    case AtomicOperationType.ModifyEntity:
                        entity = worldState.FindEntityById(op.EntityId, op.EntityType, includeDestroyed: false);
                        if (entity == null)
                        {
                            Log.Warning($"原子操作 Modify: 实体 '{op.EntityType}:{op.EntityId}' 未找到或已被销毁。");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(op.AttributeKey) || op.ModifyOperator == null)
                        {
                            Log.Error($"原子操作 Modify: 实体 '{entity.TypedId}' 的 AttributeKey 或 ModifyOperator 无效。");
                            continue;
                        }

                        entity.ModifyAttribute(op.AttributeKey, op.ModifyOperator.Value, op.ModifyValue);
                        Log.Debug($"原子操作 Modify: 实体 '{entity.TypedId}' 的属性 '{op.AttributeKey}' 已修改。");
                        changedEntityIds.Add(entity.EntityId);
                        break;

                    case AtomicOperationType.DeleteEntity:
                        entity = worldState.FindEntityById(op.EntityId, op.EntityType, includeDestroyed: false);
                        if (entity == null)
                        {
                            Log.Warning($"原子操作 Delete: 实体 '{op.EntityType}:{op.EntityId}' 未找到或已被销毁。");
                            continue;
                        }

                        entity.IsDestroyed = true;
                        Log.Debug($"原子操作 Delete: 实体 '{entity.TypedId}' 已标记为销毁。");
                        changedEntityIds.Add(entity.EntityId);
                        break;
                    default:
                        Log.Error($"未知的原子操作类型: {op.OperationType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"执行原子操作 {op.OperationType} ({op.EntityType}:{op.EntityId}) 时发生异常: {ex.Message}");
            }
        }

        return changedEntityIds.ToList();
    }

    // --- DTO Mapping Helpers ---
    private BlockSummaryDto MapToSummaryDto(Block block)
    {
        return new BlockSummaryDto
        {
            BlockId = block.BlockId,
            ParentBlockId = block.ParentBlockId,
            Status = block.Status,
            SelectedChildIndex = block.SelectedChildIndex,
            ContentSummary = GetContentSummary(block.BlockContent),
            // Safely access CreationTime from metadata
            CreationTime = block.Metadata.TryGetValue("CreationTime", out var timeObj) && timeObj is DateTime time
                ? time
                : DateTime.MinValue
        };
    }

    private BlockDetailDto MapToDetailDto(Block block)
    {
        return new BlockDetailDto
        {
            BlockId = block.BlockId,
            ParentBlockId = block.ParentBlockId,
            Status = block.Status,
            SelectedChildIndex = block.SelectedChildIndex,
            ContentSummary = GetContentSummary(block.BlockContent),
            CreationTime = block.Metadata.TryGetValue("CreationTime", out var timeObj) && timeObj is DateTime time
                ? time
                : DateTime.MinValue,
            BlockContent = block.BlockContent,
            Metadata = new Dictionary<string, object?>(block.Metadata), // Return copy
            ChildrenInfo = new Dictionary<int, string>(block.ChildrenInfo) // Return copy
        };
    }

    private string GetContentSummary(string? content, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        return content.Length <= maxLength ? content : content.Substring(0, maxLength) + "...";
    }
}
// --- END OF FILE BlockManager.cs ---