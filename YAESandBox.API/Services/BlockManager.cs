using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nito.AsyncEx;
using YAESandBox.API.Controllers; // For AsyncLock
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

    Task<bool> SetBlockStatusAsync(string blockId, BlockStatus newStatus, WorldState? resultingWsPostAI = null,
        WorldState? resultingWsPostUser = null); // Workflow 调用

    Task<List<AtomicOperation>> GetAndClearPendingCommandsAsync(string blockId); // Workflow 调用
    Task ApplyPendingCommandsAsync(string blockId, List<AtomicOperation> resolvedCommands); // 用于冲突解决后

    Task AddChildBlockRecordAsync(string parentBlockId, string childBlockId,
        Dictionary<string, object?> triggerParams); // Workflow 调用

    Task<WorldState?> GetWsInputForNewBlockAsync(string parentBlockId); // Workflow 调用
    Task<GameState?> GetGsForNewBlockAsync(string parentBlockId); // Workflow 调用
}

public class BlockManager : IBlockManager
{
    // 使用 ConcurrentDictionary 保证基本的读写线程安全
    private readonly ConcurrentDictionary<string, Block> _blocks = new();

    // 使用 AsyncLock 保护需要原子性的复杂操作 (如创建子 Block 并更新父 Block)
    private readonly ConcurrentDictionary<string, AsyncLock> _blockLocks = new(); // 每个 Block 一个锁

    private readonly INotifierService _notifierService;
    // private readonly IAtomicExecutorService _atomicExecutor; // 如果拆分执行逻辑

    public BlockManager(INotifierService notifierService /*, IAtomicExecutorService atomicExecutor */)
    {
        _notifierService = notifierService;
        // _atomicExecutor = atomicExecutor;

        // TODO: 初始化时可能需要从持久化存储加载 Blocks
        // LoadBlocksFromPersistenceAsync().GetAwaiter().GetResult();
        if (_blocks.IsEmpty)
        {
            Log.Info("BlockManager 初始化：没有从持久化存储加载任何 Block，将创建默认根 Block。");
            // 如果没有加载到任何 Block，创建一个默认的根 Block
            CreateDefaultRootBlockIfNeededAsync().GetAwaiter().GetResult();
        }
        else
        {
            Log.Info($"BlockManager 初始化：从持久化存储加载了 {_blocks.Count} 个 Block。");
        }
    }

    private async Task CreateDefaultRootBlockIfNeededAsync()
    {
        if (_blocks.IsEmpty)
        {
            var rootId = "root_0";
            Log.Warning($"未找到任何 Block，创建默认根 Block: {rootId}");
            var initialWs = new WorldState(); // 创建一个空的 WorldState
            var initialGs = new GameState(); // 创建一个空的 GameState
            initialGs["FocusCharacter"] = "player_default"; // 示例 GameState 设置
            await CreateRootBlockAsync(initialWs, initialGs);
        }
    }

    private AsyncLock GetLockForBlock(string blockId)
    {
        // 获取或创建该 Block ID 对应的锁
        return _blockLocks.GetOrAdd(blockId, _ => new AsyncLock());
    }

    public async Task<string> CreateRootBlockAsync(WorldState initialWorldState, GameState initialGameState)
    {
        var blockId = $"root_{Guid.NewGuid().ToString("N")[..8]}"; // 更唯一的根 ID
        var rootBlock = new Block(blockId, initialWorldState, initialGameState);
        rootBlock.Metadata["IsRoot"] = true;

        // 使用锁确保添加操作的原子性（虽然对于根节点并发可能性小）
        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (_blocks.TryAdd(blockId, rootBlock))
            {
                Log.Info($"根 Block '{blockId}' 已创建并添加。");
                await _notifierService.NotifyBlockStatusUpdateAsync(blockId, rootBlock.Status);
                // TODO: 持久化存储新创建的 Block
                return blockId;
            }
            else
            {
                Log.Error($"尝试创建根 Block '{blockId}' 失败，可能已存在同名 Block。");
                // 理论上 Guid 不会重复，但以防万一
                throw new InvalidOperationException($"Failed to add root block '{blockId}'. It might already exist.");
            }
        }
    }

    public Task<Block?> GetBlockAsync(string blockId)
    {
        _blocks.TryGetValue(blockId, out var block);
        return Task.FromResult(block); // 直接返回，ConcurrentDictionary 的读取是线程安全的
    }

    public async Task<BlockSummaryDto?> GetBlockSummaryDtoAsync(string blockId)
    {
        var block = await GetBlockAsync(blockId);
        return block == null ? null : MapToSummaryDto(block);
    }

    public Task<IEnumerable<BlockSummaryDto>> GetAllBlockSummariesAsync()
    {
        // ConcurrentDictionary 的 Values 获取快照，是线程安全的
        var summaries = _blocks.Values.Select(MapToSummaryDto).OrderBy(b => (DateTime?)b.CreationTime).ToList();
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
        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (!_blocks.TryGetValue(blockId, out var block))
            {
                // 返回明确的 NotFound 结果
                return (UpdateResult.NotFound, $"Block with ID '{blockId}' not found.");
            }

            if (selectedChildIndex >= 0 && selectedChildIndex < block.ChildrenInfo.Count)
            {
                block.SelectedChildIndex = selectedChildIndex;
                Log.Debug($"Block '{blockId}': Selected child index set to {selectedChildIndex}.");
                // TODO: 持久化 Block 的变更
                // 返回成功结果
                return (UpdateResult.Success, null);
            }
            else
            {
                Log.Warning(
                    $"Block '{blockId}': Invalid child index {selectedChildIndex} provided. Valid range: 0 to {block.ChildrenInfo.Count - 1}.");
                // 返回明确的无效操作结果 (或者可以认为是 Conflict/BadRequest?)
                // 使用 InvalidOperation 可能更贴切内部逻辑错误，BadRequest 更符合 API 层面
                // 暂定用 InvalidOperation，让 Controller 转为 BadRequest
                return (UpdateResult.InvalidOperation,
                    $"Invalid child index '{selectedChildIndex}'. Valid range: 0 to {block.ChildrenInfo.Count - 1}.");
            }
        }
    }


    public async Task<GameState?> GetBlockGameStateAsync(string blockId)
    {
        var block = await GetBlockAsync(blockId);
        // GameState 本身是引用类型，返回的是引用，但 Clone() 方法可以获取副本
        // 直接返回引用允许修改，如果需要只读，应返回 Clone()
        return block?.GameState; // 直接返回引用
    }

    public async Task<UpdateResult> UpdateBlockGameStateAsync(string blockId,
        Dictionary<string, object?> settingsToUpdate)
    {
        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (!_blocks.TryGetValue(blockId, out var block))
            {
                return UpdateResult.NotFound;
            }

            // 检查状态是否允许修改 GameState
            if (block.Status == BlockStatus.Loading || block.Status == BlockStatus.ResolvingConflict)
            {
                Log.Warning($"尝试在 Block '{blockId}' 处于 '{block.Status}' 状态时修改 GameState，已拒绝。");
                return UpdateResult.Conflict; // 不允许在加载或冲突解决期间修改
            }

            foreach (var kvp in settingsToUpdate)
            {
                block.GameState[kvp.Key] = kvp.Value; // 直接修改 GameState
            }

            Log.Debug($"Block '{blockId}': GameState 已更新。");
            // TODO: 持久化 Block (因为 GameState 变了)
            // 注意：此变更通常不直接影响 WorldState，可能不需要 NotifyStateUpdateAsync
            return UpdateResult.Success;
        }
    }


    public async Task<IEnumerable<BaseEntity>?> GetAllEntitiesSummaryAsync(string blockId)
    {
        var block = await GetBlockAsync(blockId);
        if (block == null) return null;

        try
        {
            var targetWs = block.GetTargetWorldStateForInteraction();
            // 返回所有未销毁的实体
            return targetWs.Items.Values.Cast<BaseEntity>()
                .Concat(targetWs.Characters.Values)
                .Concat(targetWs.Places.Values)
                .Where(e => !e.IsDestroyed)
                .ToList(); // 创建副本
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Block '{blockId}': 获取实体摘要时出错: {ex.Message}");
            return null;
        }
    }

    public async Task<BaseEntity?> GetEntityDetailAsync(string blockId, TypedID entityRef)
    {
        var block = await GetBlockAsync(blockId);
        if (block == null) return null;

        try
        {
            var targetWs = block.GetTargetWorldStateForInteraction();
            return targetWs.FindEntity(entityRef, includeDestroyed: false); // 查找未销毁的
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
        // 使用锁确保状态检查和操作执行/入队的原子性
        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (!_blocks.TryGetValue(blockId, out var block))
            {
                return AtomicExecutionResult.NotFound;
            }

            switch (block.Status)
            {
                case BlockStatus.Idle:
                case BlockStatus.Error: // 允许在错误状态下尝试修改（可能用于修复）？ 暂定允许
                    try
                    {
                        var targetWs = block.GetTargetWorldStateForInteraction(); // 获取 WsPostUser
                        var changedEntityIds = ExecuteAtomicOperations(targetWs, operations); // 直接执行
                        Log.Debug($"Block '{blockId}' ({block.Status}): 原子操作已执行，影响 {changedEntityIds.Count} 个实体。");
                        // TODO: 持久化 Block (因为 WsPostUser 变了)

                        // 通知状态变更
                        if (changedEntityIds.Count > 0)
                        {
                            await _notifierService.NotifyStateUpdateAsync(blockId, changedEntityIds);
                        }

                        return AtomicExecutionResult.Executed;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Block '{blockId}': 执行原子操作时出错: {ex.Message}");
                        // 可以考虑将 Block 状态设为 Error
                        // await SetBlockStatusAsync(blockId, BlockStatus.Error);
                        return AtomicExecutionResult.Error;
                    }

                case BlockStatus.Loading:
                case BlockStatus.ResolvingConflict: // 在解决冲突状态下，新的修改也应排队
                    block.PendingUserCommands.AddRange(operations); // 暂存指令
                    Log.Debug(
                        $"Block '{blockId}' ({block.Status}): 原子操作已暂存 ({operations.Count} 条)。当前暂存总数: {block.PendingUserCommands.Count}");
                    // TODO: 是否需要持久化暂存的指令？看需求
                    return AtomicExecutionResult.Queued;

                default:
                    Log.Error($"Block '{blockId}': 处于未知的状态 '{block.Status}'，无法处理原子操作。");
                    return AtomicExecutionResult.Error; // 或者更特定的状态码
            }
        }
    }

    /// <summary>
    /// 由工作流服务调用，以更新 Block 的状态。
    /// </summary>
    public async Task<bool> SetBlockStatusAsync(string blockId, BlockStatus newStatus,
        WorldState? resultingWsPostAI = null, WorldState? resultingWsPostUser = null)
    {
        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (!_blocks.TryGetValue(blockId, out var block))
            {
                Log.Error($"尝试设置不存在的 Block '{blockId}' 的状态为 {newStatus}。");
                return false;
            }

            var oldStatus = block.Status;
            if (oldStatus == newStatus) return true; // 状态未改变

            block.Status = newStatus;
            if (resultingWsPostAI != null) block.WsPostAI = resultingWsPostAI;
            if (resultingWsPostUser != null) block.WsPostUser = resultingWsPostUser;

            // 根据状态转换清理 WsTemp
            if (newStatus == BlockStatus.Idle || newStatus == BlockStatus.Error)
            {
                block.WsTemp = null; // 清理临时状态
            }

            Log.Info($"Block '{blockId}': 状态从 {oldStatus} 变为 {newStatus}。");
            // TODO: 持久化 Block 状态和可能更新的 WorldState

            // 广播状态更新
            await _notifierService.NotifyBlockStatusUpdateAsync(blockId, newStatus);
            return true;
        }
    }

    /// <summary>
    /// 由工作流服务调用，获取并清除指定 Block 的暂存用户指令。
    /// </summary>
    public async Task<List<AtomicOperation>> GetAndClearPendingCommandsAsync(string blockId)
    {
        // 获取并清除是原子操作，需要锁
        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (_blocks.TryGetValue(blockId, out var block))
            {
                var commands = new List<AtomicOperation>(block.PendingUserCommands);
                block.PendingUserCommands.Clear();
                Log.Debug($"Block '{blockId}': 获取并清除了 {commands.Count} 条暂存指令。");
                // TODO: 如果暂存指令被持久化了，这里需要更新持久化存储
                return commands;
            }
            else
            {
                Log.Warning($"尝试获取不存在的 Block '{blockId}' 的暂存指令。");
                return new List<AtomicOperation>(); // 返回空列表
            }
        }
    }

    /// <summary>
    /// 由冲突解决流程调用，将解决后的指令应用到 WsPostAI (或 WsInput) 上，
    /// 并最终更新 WsPostUser。
    /// </summary>
    public async Task ApplyPendingCommandsAsync(string blockId, List<AtomicOperation> resolvedCommands)
    {
        using (await GetLockForBlock(blockId).LockAsync())
        {
            if (!_blocks.TryGetValue(blockId, out var block))
            {
                Log.Error($"尝试为不存在的 Block '{blockId}' 应用已解决的指令。");
                return;
            }

            if (block.Status != BlockStatus.ResolvingConflict &&
                block.Status != BlockStatus.Loading) // 可能从 Loading 直接解决
            {
                Log.Warning(
                    $"尝试在 Block '{blockId}' 处于 '{block.Status}' 状态时应用已解决的指令，已忽略。预期状态: ResolvingConflict 或 Loading。");
                return;
            }

            try
            {
                // 冲突解决后的指令应该应用在哪个基础上？
                // 理想情况是 WsPostAI (如果存在)，否则是 WsInput
                var baseWs = block.WsPostAI ?? block.WsInput;
                // 创建一个新的 WsPostUser 来应用解决后的指令
                var finalWs = baseWs.Clone();
                var changedEntityIds = ExecuteAtomicOperations(finalWs, resolvedCommands);

                block.WsPostUser = finalWs; // 更新最终的用户可见状态
                Log.Info($"Block '{blockId}': 已解决的冲突指令已应用，生成新的 WsPostUser。影响 {changedEntityIds.Count} 个实体。");

                // 清理 WsTemp （如果还存在）
                block.WsTemp = null;

                // TODO: 持久化 Block (WsPostUser 更新)

                // 将状态设置为 Idle
                await SetBlockStatusAsync(blockId, BlockStatus.Idle); // SetBlockStatus 会处理通知

                // 可能需要额外通知状态已更新
                if (changedEntityIds.Count > 0)
                {
                    await _notifierService.NotifyStateUpdateAsync(blockId, changedEntityIds);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Block '{blockId}': 应用已解决的指令时出错: {ex.Message}");
                await SetBlockStatusAsync(blockId, BlockStatus.Error); // 出错则标记为错误状态
            }
        }
    }


    /// <summary>
    /// 由工作流服务调用，在父 Block 中记录新创建的子 Block 信息。
    /// </summary>
    public async Task AddChildBlockRecordAsync(string parentBlockId, string childBlockId,
        Dictionary<string, object?> triggerParams)
    {
        using (await GetLockForBlock(parentBlockId).LockAsync()) // 锁父 Block
        {
            if (!_blocks.TryGetValue(parentBlockId, out var parentBlock))
            {
                Log.Error($"尝试向不存在的父 Block '{parentBlockId}' 添加子 Block 记录 '{childBlockId}'。");
                return; // 或者抛出异常？
            }

            int childIndex = parentBlock.ChildrenInfo.Count;
            parentBlock.ChildrenInfo[childIndex] = childBlockId;
            parentBlock.TriggeredChildParams[childBlockId] = triggerParams ?? new Dictionary<string, object?>();
            parentBlock.SelectedChildIndex = childIndex; // 默认选中新子节点

            Log.Debug($"父 Block '{parentBlockId}': 已添加子 Block '{childBlockId}' 记录，索引为 {childIndex}。");
            // TODO: 持久化父 Block 的变更
        }
    }


    /// <summary>
    /// 由工作流服务调用，获取用于创建新子 Block 的输入 WorldState (父节点的 WsPostUser 的克隆)。
    /// </summary>
    public async Task<WorldState?> GetWsInputForNewBlockAsync(string parentBlockId)
    {
        // 读取父节点的 WsPostUser 不需要锁，但要确保 Block 存在
        var parentBlock = await GetBlockAsync(parentBlockId);
        if (parentBlock?.WsPostUser == null)
        {
            Log.Error($"父 Block '{parentBlockId}' 不存在或其 WsPostUser 为 null，无法为新子 Block 提供输入 WorldState。");
            return null;
        }

        return parentBlock.WsPostUser.Clone(); // 返回克隆副本
    }

    /// <summary>
    /// 由工作流服务调用，获取用于创建新子 Block 的 GameState (父节点的 GameState 的克隆)。
    /// </summary>
    public async Task<GameState?> GetGsForNewBlockAsync(string parentBlockId)
    {
        var parentBlock = await GetBlockAsync(parentBlockId);
        if (parentBlock?.GameState == null)
        {
            Log.Error($"父 Block '{parentBlockId}' 不存在或其 GameState 为 null，无法为新子 Block 提供输入 GameState。");
            return null;
        }

        return parentBlock.GameState.Clone(); // 返回克隆副本
    }


    // --- 私有辅助方法 ---

    /// <summary>
    /// 在给定的 WorldState 上执行一批原子操作。
    /// </summary>
    /// <returns>发生变更的实体 ID 列表。</returns>
    private List<string> ExecuteAtomicOperations(WorldState worldState, IEnumerable<AtomicOperation> operations)
    {
        var changedEntityIds = new HashSet<string>(); // 使用 HashSet 避免重复

        foreach (var op in operations)
        {
            try
            {
                BaseEntity? entity;
                switch (op.OperationType)
                {
                    case AtomicOperationType.CreateEntity:
                        // 检查是否已存在（即使是销毁的也可能需要处理）
                        entity = worldState.FindEntityById(op.EntityId, op.EntityType, includeDestroyed: true);
                        if (entity != null && !entity.IsDestroyed)
                        {
                            Log.Warning($"原子操作 Create: 实体 '{op.EntityType}:{op.EntityId}' 已存在且未销毁，将被覆盖。");
                        }

                        // 创建新实例
                        entity = op.EntityType switch
                        {
                            EntityType.Item => new Item(op.EntityId),
                            EntityType.Character => new Character(op.EntityId),
                            EntityType.Place => new Place(op.EntityId),
                            _ => throw new ArgumentOutOfRangeException($"不支持创建类型: {op.EntityType}")
                        };
                        // 应用初始属性
                        if (op.InitialAttributes != null)
                        {
                            foreach (var attr in op.InitialAttributes)
                            {
                                entity.SetAttribute(attr.Key, attr.Value); // 使用 SetAttribute 进行基础验证
                            }
                        }

                        worldState.AddEntity(entity);
                        Log.Debug($"原子操作 Create: 成功创建/覆盖实体 '{entity.TypedId}'。");
                        changedEntityIds.Add(entity.EntityId);
                        break;

                    case AtomicOperationType.ModifyEntity:
                        entity = worldState.FindEntityById(op.EntityId, op.EntityType,
                            includeDestroyed: false); // 只修改未销毁的
                        if (entity == null)
                        {
                            Log.Warning($"原子操作 Modify: 实体 '{op.EntityType}:{op.EntityId}' 未找到或已被销毁。");
                            continue; // 跳过此操作
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
                        entity = worldState.FindEntityById(op.EntityId, op.EntityType,
                            includeDestroyed: false); // 查找未销毁的
                        if (entity == null)
                        {
                            Log.Warning($"原子操作 Delete: 实体 '{op.EntityType}:{op.EntityId}' 未找到或已被销毁。");
                            continue; // 已经是目标状态
                        }

                        entity.IsDestroyed = true; // 标记为销毁
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
                // 根据策略，可以选择继续执行其他操作或直接抛出异常中止整个批次
                // 这里选择继续执行其他操作，但记录错误
            }
        }

        return changedEntityIds.ToList();
    }


    // --- DTO Mapping Helpers (可以移到专门的 Mapper 类) ---
    private BlockSummaryDto MapToSummaryDto(Block block)
    {
        return new BlockSummaryDto
        {
            BlockId = block.BlockId,
            ParentBlockId = block.ParentBlockId,
            Status = block.Status,
            SelectedChildIndex = block.SelectedChildIndex,
            // 尝试从内容或元数据获取摘要
            ContentSummary = GetContentSummary(block.BlockContent), // 实现这个方法
            CreationTime = (DateTime)(block.Metadata.GetValueOrDefault("CreationTime") ?? DateTime.MinValue)
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
            CreationTime = (DateTime)(block.Metadata.GetValueOrDefault("CreationTime") ?? DateTime.MinValue),
            BlockContent = block.BlockContent,
            Metadata = new Dictionary<string, object?>(block.Metadata), // 返回副本
            ChildrenInfo = new Dictionary<int, string>(block.ChildrenInfo) // 返回副本
        };
    }

    private string GetContentSummary(string content, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        // 简单的截断，可以根据内容类型（如 JSON/HTML）实现更智能的摘要
        return content.Length <= maxLength ? content : content.Substring(0, maxLength) + "...";
    }
}