using System.Collections.Concurrent;
using System.Text.Json;
using FluentResults;
using Nito.AsyncEx;
using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;

namespace YAESandBox.Core.Block;

public partial class BlockManager : IBlockManager
{
    
    public const string DEBUG_WorkFlowName = "";
    
    /// <summary>
    /// 构造函数，创建默认根节点。
    /// </summary>
    /// <exception cref="Exception"></exception>
    public BlockManager()
    {
        var rootBlock = Block.CreateBlock(WorldRootId, null, DEBUG_WorkFlowName, new WorldState(), new GameState());
        if (this.blocks.TryAdd(WorldRootId, rootBlock.ForceIdleState()))
        {
            Log.Info("BlockManager: 根节点已创建并设置为空闲。");
        }
        else
            throw new Exception($"添加默认根 Block '{WorldRootId}' 失败。");
    }

    /// <summary>
    /// 所有根节点的父节点，一个超级根节点。如果创建节点时没有提供父节点，则自动创建到它下面。
    /// </summary>
    public const string WorldRootId = "__WORLD__";

    private ConcurrentDictionary<string, BlockStatus> blocks { get; } = new();
    private ConcurrentDictionary<string, AsyncLock> blockLocks { get; } = new(); // 并发控制

    /// <summary>
    /// 用于控制对单个 Block 的并发访问。
    /// </summary>
    /// <returns></returns>
    private AsyncLock GetLockForBlock(string blockId) => this.blockLocks.GetOrAdd(blockId, _ => new AsyncLock());

    // /// <summary>
    // /// 全局锁，用于控制对单个 BlockManager 的并发访问。
    // /// </summary>
    // private AsyncLock globalLoadLock { get; } = new AsyncLock();

    public IReadOnlyDictionary<string, Block> GetBlocks() =>
        this.blocks.ToDictionary(kv => kv.Key, kv => kv.Value.Block);

    public IReadOnlyDictionary<string, IBlockNode> GetNodeOnlyBlocks() =>
        this.blocks.ToDictionary(kv => kv.Key, IBlockNode (kv) => kv.Value.Block);

    /// <summary>
    /// 创建子Block，需要父BlockId和触发参数，以及触发所用的工作流的名称
    /// </summary>
    /// <param name="parentBlockId"></param>
    /// <param name="workFlowName"></param>
    /// <param name="triggerParams"></param>
    /// <returns></returns>
    public async Task<LoadingBlockStatus?> CreateChildBlock_Async(
        string? parentBlockId, string workFlowName, Dictionary<string, object?> triggerParams)
    {
        parentBlockId ??= WorldRootId;
        using (await this.GetLockForBlock(parentBlockId).LockAsync()) // Lock parent to add child info
        {
            // 1. Get Parent Block
            if (!this.blocks.TryGetValue(parentBlockId, out var parentBlock))
            {
                Log.Error($"Cannot create child block: Parent block '{parentBlockId}' not found.");
                return null;
            }

            // 2. Create the new Block instance (use internal constructor or dedicated method)
            if (parentBlock is not IdleBlockStatus idleParentBlock)
            {
                Log.Error($"Cannot create child block: Parent block '{parentBlockId}' is not idle.");
                return null;
            }

            parentBlock.Block.TriggeredChildParams = triggerParams;

            (string newBlockId, var newChildBlock) = idleParentBlock.CreateNewChildrenBlock(workFlowName);


            // 4. Add New Block to Manager's Dictionary (Lock the *new* block ID)
            using (await this.GetLockForBlock(newBlockId).LockAsync())
            {
                if (!this.blocks.TryAdd(newBlockId, newChildBlock))
                {
                    Log.Error($"添加新 Block '{newBlockId}' 失败，可能已存在同名 Block。这个概率是地球沙子数量的平方，恭喜你中大奖了！（也有可能是随机数生成器坏了？）");
                    return null;
                }

                // 3. Add Child Info to Parent
                parentBlock.Block.AddChildren(newChildBlock.Block);
                Log.Debug($"父 Block '{parentBlockId}': 已添加子 Block '{newBlockId}' 记录。");
                // TODO: 持久化 Parent Block changes

                Log.Info($"新 Block '{newBlockId}' 已创建并添加，状态: {newChildBlock.GetType()}。");
                // TODO: 持久化 New Block
            }

            return newChildBlock;
        }
    }

    /// <inheritdoc/>
    public async Task<BlockResultCode> UpdateBlockDetailsAsync(string blockId, string? newContent,
        Dictionary<string, string?>? metadataUpdates)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var blockStatus))
            {
                return BlockResultCode.NotFound;
            }

            // *** 关键：只允许在 Idle 状态下修改 ***
            if (blockStatus is not IdleBlockStatus idleBlock)
            {
                Log.Warning($"尝试修改 Block '{blockId}' 的内容/元数据，但其状态为 {blockStatus.StatusCode} (非 Idle)。操作被拒绝。");
                return BlockResultCode.InvalidState; // 返回新的状态码
            }

            bool updated = false;

            // 更新 Content
            if (newContent != null)
            {
                idleBlock.Block.BlockContent = newContent;
                Log.Debug($"Block '{blockId}': BlockContent 已更新。");
                updated = true;
            }

            // 更新 Metadata
            if (metadataUpdates != null)
            {
                foreach (var kvp in metadataUpdates)
                {
                    if (kvp.Value == null) // 值为 null 表示移除
                    {
                        if (!idleBlock.Block.RemoveMetaData(kvp.Key)) continue;
                        Log.Debug($"Block '{blockId}': 元数据 '{kvp.Key}' 已移除。");
                    }
                    else // 非 null 值表示添加或更新
                    {
                        // 使用现有的 AddOrSetMetaData
                        idleBlock.Block.AddOrSetMetaData(kvp.Key, kvp.Value);
                        Log.Debug($"Block '{blockId}': 元数据 '{kvp.Key}' 已设置。");
                    }

                    updated = true;
                }
            }

            if (updated)
            {
                Log.Info($"Block '{blockId}': 内容或元数据已成功更新。");
                // 可以在这里添加持久化逻辑，如果需要立即保存这些更改
                // await PersistBlockAsync(idleBlock.Block);

                // 决定是否发送通知。目前我们不发送 SignalR 通知。
                // 如果需要，可以在此调用 notifierService.NotifyStateUpdateSignal(blockId);
            }
            else
            {
                Log.Debug($"Block '{blockId}': 收到更新请求，但没有提供有效的更新内容或元数据。");
            }

            return BlockResultCode.Success;
        }
    }

    /// <summary>
    /// 获取从根节点到指定块ID可达的最深层叶子节点（根据“最后一个子节点”规则）的完整路径。
    /// 该方法首先确定从起始块向下到最深叶子的路径，然后反向追溯到根节点。
    /// </summary>
    /// <param name="startBlockId">起始块的ID。假定此ID在 'blocks' 字典中有效。</param>
    /// <returns>一个包含从根节点到最深层叶子节点ID的列表。如果路径中遇到数据不一致（如引用了不存在的块），则记录错误并返回空列表。</returns>
    public List<string> GetPathToRoot(string startBlockId)
    {
        // --- 阶段 1: 查找从 startBlockId 出发，遵循“最后一个子节点”规则到达的最深叶节点 ---
        string currentId = startBlockId; // 当前遍历的节点ID

        // 循环向下查找，直到遇到没有子节点的块 或 遇到数据不一致的情况
        while (this.blocks.TryGetValue(currentId, out var currentBlock))
        {
            if (!currentBlock.Block.ChildrenList.Any())
                break;

            string lastChildId = currentBlock.Block.ChildrenList.Last(); // 获取最后一个子节点的ID
            currentId = lastChildId; // 移动到最后一个子节点
            // 在下一次循环的 TryGetValue 中会检查 lastChildId 是否有效
            // 如果 lastChildId 在字典中不存在，TryGetValue 会返回 false，循环终止

            // 如果当前块有多个子块，记录日志
            if (currentBlock.Block.ChildrenList.Count > 1)
                Log.Info($"块 '{currentId}' 存在多个子块，自动选择最后一个子块 '{lastChildId}'。");
        }

        // 循环结束后，'currentId' 就是我们找到的最深叶子节点的ID
        // 但我们需要确认这个 'currentId' 自身是有效的（它可能来自一个无效的 lastChildId 引用）
        if (!this.blocks.ContainsKey(currentId))
        {
            // 这种情况理论上只会在最后一个 lastChildId 无效时发生
            Log.Error($"数据不一致：尝试访问的子块 '{currentId}' 在字典中不存在。");
            return []; // 数据有问题，无法构建路径
        }

        string deepestLeafId = currentId; // 确认最深的叶子ID
        // 初始化最深叶子节点为起始节点

        // --- 阶段 2: 从最深的叶节点向上回溯到根节点 ---
        var path = new List<string>(); // 用于存储从叶子到根的路径（稍后反转）
        string? idToTrace = deepestLeafId; // 从最深的叶子开始向上追溯

        // 循环向上查找父节点，直到 ParentBlockId 为 null (到达根节点) 或 遇到数据不一致
        while (idToTrace != null)
        {
            // 尝试获取当前节点的数据
            if (!this.blocks.TryGetValue(idToTrace, out var blockData))
            {
                // 如果 idToTrace 不为 null 但在字典中找不到，说明数据存在不一致性
                Log.Error($"数据不一致：块 '{path.LastOrDefault() ?? deepestLeafId}' 的父块ID '{idToTrace}' 指向一个不存在的块。");
                return []; // 返回空列表表示失败
            }

            path.Add(idToTrace); // 将当前节点ID添加到路径中
            idToTrace = blockData.Block.ParentBlockId; // 移动到父节点
        }

        // --- 阶段 3: 反转路径，使其从根节点开始 ---
        path.Reverse(); // 现在路径是从 根 -> ... -> 最深叶子

        return path;
    }

    /// <summary>
    /// 获取BlockID对应的块
    /// </summary>
    /// <param name="blockId"></param>
    /// <returns></returns>
    public Task<BlockStatus?> GetBlockAsync(string blockId)
    {
        Log.Debug($"GetBlockAsync: 尝试获取 Block ID: '{blockId}'");
        this.blocks.TryGetValue(blockId, out var block);
        if (block == null)
            Log.Warning(
                $"GetBlockAsync: 未在 _blocks 字典中找到 Block ID: '{blockId}'。当前字典大小: {this.blocks.Count}");
        else
            Log.Debug($"GetBlockAsync: 成功找到 Block ID: '{blockId}'，状态为: {block.StatusCode}");

        return Task.FromResult(block);
    }

    /// <summary>
    /// 更新block的GameState，它比worldState简单很多，所以简单加锁即可。
    /// 它完全不被一等工作流修改，所以无论BlockStatus是什么，都可以进行修改，不存在任何的冲突。
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="settingsToUpdate"></param>
    /// <returns></returns>
    public async Task<UpdateResult> UpdateBlockGameStateAsync(
        string blockId, Dictionary<string, object?> settingsToUpdate)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
                return UpdateResult.NotFound;

            foreach (var kvp in settingsToUpdate)
                block.Block.GameState[kvp.Key] = kvp.Value;

            Log.Debug($"Block '{blockId}': GameState 已更新。");
            return UpdateResult.Success;
        }
    }

    /// <summary>
    /// 异步执行或排队原子操作。
    /// 如果为IdleBlockStatus和LoadingBlockStatus，则执行（并在 Loading 期间排队）。
    /// 如果为ConflictBlockStatus或ErrorBlockStatus，则不执行任何东西。
    /// </summary>
    /// <param name="blockId">区块唯一标识符</param>
    /// <param name="operations">待执行的原子操作列表</param>
    /// <returns>返回一个元组，包含区块状态和操作结果列表</returns>
    public async Task<(Result<IEnumerable<AtomicOperation>> result, BlockStatusCode? blockStatusCode)>
        EnqueueOrExecuteAtomicOperationsAsync(string blockId, List<AtomicOperation> operations)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
                return (Result.Fail($"尝试执行原子操作失败: Block '{blockId}' 未找到。"), null);

            var result = block switch
            {
                LoadingBlockStatus loading => loading.ApplyOperations(operations),
                IdleBlockStatus idle => idle.ApplyOperations(operations),
                // ConflictBlockStatus conflict => BlockStatusError.Conflict(conflict, $"Block '{blockId}' 状态为 Conflict。").ToResult(),
                // ErrorBlockStatus error => BlockStatusError.Error(error, $"Block '{blockId}' 状态为 Error。").ToResult(),
                _ => BlockStatusError.Error(block, $"尝试执行原子操作失败: Block '{blockId}' 状态为 {block.StatusCode}。").ToResult()
            };

            return (result, block.StatusCode);
        }
    }

    #region 持久化逻辑

    /// <summary>
    /// 将当前 BlockManager 的状态保存到流中。
    /// </summary>
    /// <param name="stream">要写入的流。</param>
    /// <param name="frontEndBlindData">前端提供的盲存数据。</param>
    public async Task SaveToFileAsync(Stream stream, object? frontEndBlindData)
    {
        var archive = new ArchiveDto
        {
            BlindStorage = frontEndBlindData,
            ArchiveVersion = "1.0" // Or read from config/constant
        };

        // 遍历内存中的 Blocks
        foreach (var kvp in this.blocks)
        {
            var blockId = kvp.Key;
            var blockStatus = kvp.Value; // This is BlockStatus (Idle, Loading etc.)
            var coreBlock = blockStatus.Block; // The actual Block instance

            var blockDto = new BlockDto
            {
                BlockId = coreBlock.BlockId,
                ParentBlockId = coreBlock.ParentBlockId,
                WorkFlowName = coreBlock.WorkFlowName,
                ChildrenIds = new List<string>(coreBlock.ChildrenList), // Copy list
                BlockContent = coreBlock.BlockContent,
                Metadata = coreBlock.Metadata.ToDictionary(entry => entry.Key,
                    entry => entry.Value), // Shallow copy Metadata
                TriggeredChildParams =
                    coreBlock.TriggeredChildParams.ToDictionary(entry => entry.Key,
                        entry => entry.Value), // Shallow copy Params
                GameState = coreBlock.GameState.GetAllSettings()
                    .ToDictionary(entry => entry.Key, entry => entry.Value)
            };

            blockDto.WorldStates["wsInput"] = this.MapWorldStateDto(coreBlock.wsInput);
            if (coreBlock.wsPostAI != null)
                blockDto.WorldStates["wsPostAI"] = this.MapWorldStateDto(coreBlock.wsPostAI);
            if (coreBlock.wsPostUser != null)
                blockDto.WorldStates["wsPostUser"] = this.MapWorldStateDto(coreBlock.wsPostUser);
            // wsTemp 不保存

            archive.Blocks.Add(blockId, blockDto);
        }

        await JsonSerializer.SerializeAsync(stream, archive, PersistenceMapper.JsonOptions);
        Log.Info($"BlockManager state saved. Blocks count: {archive.Blocks.Count}");
    }

    /// <summary>
    /// 从流中加载 BlockManager 的状态。
    /// </summary>
    /// <param name="stream">要读取的流。</param>
    /// <returns>恢复的前端盲存数据。</returns>
    public async Task<object?> LoadFromFileAsync(Stream stream)
    {
        var archive = await JsonSerializer.DeserializeAsync<ArchiveDto>(stream, PersistenceMapper.JsonOptions);

        if (archive == null)
        {
            Log.Error("Failed to deserialize archive or archive is empty.");
            // Handle error as needed (e.g., throw, return default)
            // Since we ignore most errors, just log and potentially start fresh.
            this.blocks.Clear();
            this.blockLocks.Clear();
            // Ensure root block exists if starting fresh
            if (!this.blocks.ContainsKey(WorldRootId))
            {
                this.blocks.TryAdd(WorldRootId,
                    Block.CreateBlock(WorldRootId, null, DEBUG_WorkFlowName, new WorldState(), new GameState()));
            }

            return null;
        }

        Log.Info(
            $"Loading BlockManager state from archive version {archive.ArchiveVersion}. Blocks count: {archive.Blocks.Count}");

        // --- 清空当前状态并重建 ---
        var newBlocks = new ConcurrentDictionary<string, BlockStatus>();
        var newLocks = new ConcurrentDictionary<string, AsyncLock>();

        foreach ((string? blockId, var blockDto) in archive.Blocks)
        {
            // --- 恢复状态 ---
            var gameState = PersistenceMapper.MapGameState(blockDto.GameState);
            // Metadata and TriggeredChildParams might need deep copy or type recovery if complex objects are stored
            var metadata = blockDto.Metadata.ToDictionary();
            var triggeredParams = blockDto.TriggeredChildParams.ToDictionary(entry => entry.Key,
                entry => PersistenceMapper.DeserializeObjectValue(entry.Value));

            // --- 恢复 WorldState 快照 ---
            var wsInput = PersistenceMapper.MapWorldState(blockDto.WorldStates.GetValueOrDefault("wsInput"));
            var wsPostAI = PersistenceMapper.MapWorldState(blockDto.WorldStates.GetValueOrDefault("wsPostAI"));
            var wsPostUser = PersistenceMapper.MapWorldState(blockDto.WorldStates.GetValueOrDefault("wsPostUser"));

            if (wsInput == null && blockId != WorldRootId) // Root might start empty, others must have wsInput
            {
                Log.Error($"Block '{blockId}' loaded without wsInput data. Skipping block.");
                continue; // Skip this block if essential wsInput is missing
            }

            wsInput ??= new WorldState(); // Ensure root has a non-null wsInput


            // --- 创建 Block 实例 (使用新的构造函数) ---
            IdleBlockStatus coreBlock;
            try
            {
                coreBlock = Block.CreateBlockFromSave(
                    blockId,
                    blockDto.ParentBlockId,
                    blockDto.WorkFlowName,
                    blockDto.ChildrenIds,
                    blockDto.BlockContent,
                    metadata,
                    triggeredParams,
                    gameState,
                    wsInput, // wsInput is mandatory now (except maybe root)
                    wsPostAI,
                    wsPostUser
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error creating Block instance for ID '{blockId}' during load. Skipping block.");
                continue;
            }


            // --- 创建 BlockStatus (强制为 Idle) ---
            // 确保 Idle 状态所需的状态是存在的 (wsPostUser)
            if (coreBlock.Block.wsPostUser == null)
            {
                if (coreBlock.Block.wsPostAI != null)
                {
                    coreBlock.Block.wsPostUser = coreBlock.Block.wsPostAI.Clone(); // Prefer wsPostAI if available
                    Log.Warning($"Block '{blockId}' loaded as Idle but wsPostUser was null. Recovered from wsPostAI.");
                }
                else
                {
                    coreBlock.Block.wsPostUser = coreBlock.Block.wsInput.Clone(); // Fallback to wsInput
                    Log.Warning($"Block '{blockId}' loaded as Idle but wsPostUser was null. Recovered from wsInput.");
                }
            }

            if (!newBlocks.TryAdd(blockId, coreBlock))
            {
                Log.Error($"Failed to add loaded block '{blockId}' to dictionary (duplicate?). Skipping.");
                continue;
            }

            newLocks.GetOrAdd(blockId, _ => new AsyncLock()); // Ensure lock exists
        }

        // --- 原子地替换 BlockManager 的状态 ---
        // (简单替换，如果需要更复杂的事务性替换，逻辑会更复杂)
        this.blocks.Clear();
        this.blockLocks.Clear();
        foreach (var kvp in newBlocks) this.blocks.TryAdd(kvp.Key, kvp.Value);
        foreach (var kvp in newLocks) this.blockLocks.TryAdd(kvp.Key, kvp.Value);


        Log.Info($"BlockManager state loaded successfully. Final block count: {this.blocks.Count}");
        return archive.BlindStorage; // 返回盲存数据
    }

    // Helper to map WorldState to DTO (needs implementation)
    private WorldStateDto MapWorldStateDto(WorldState ws)
    {
        var dto = new WorldStateDto();
        this.MapEntitiesDto(dto.Items, ws.Items);
        this.MapEntitiesDto(dto.Characters, ws.Characters);
        this.MapEntitiesDto(dto.Places, ws.Places);
        return dto;
    }

    private void MapEntitiesDto<TEntity>(Dictionary<string, EntityDto> targetDict,
        Dictionary<string, TEntity> sourceDict)
        where TEntity : BaseEntity
    {
        foreach (var kvp in sourceDict)
        {
            // 在序列化时，不需要担心 object? 问题，System.Text.Json 会处理
            targetDict.Add(kvp.Key, new EntityDto { Attributes = kvp.Value.GetAllAttributes() });
        }
    }

    #endregion

    #region 内部操作方法

    /// <summary>
    /// (内部实现) 手动创建新的 Idle Block。
    /// </summary>
    public async Task<(ManagementResult result, BlockStatus? newBlockStatus)> InternalCreateBlockManuallyAsync(
        string parentBlockId, Dictionary<string, string>? initialMetadata)
    {
        // 1. 验证父 Block
        if (!this.blocks.TryGetValue(parentBlockId, out var parentBlockStatus))
        {
            Log.Warning($"手动创建 Block 失败: 父 Block '{parentBlockId}' 未找到。");
            return (ManagementResult.NotFound, null);
        }

        if (parentBlockStatus is not IdleBlockStatus)
        {
            Log.Warning($"手动创建 Block 失败: 父 Block '{parentBlockId}' 状态为 {parentBlockStatus.StatusCode}，非 Idle。");
            return (ManagementResult.InvalidState, null);
        }

        // 2. 生成新 Block ID
        string newBlockId = $"manual_blk_{Guid.NewGuid().ToString("N")[..8]}";
        Log.Info($"准备手动创建新 Block '{newBlockId}'，父节点: '{parentBlockId}'。");

        // 3. 获取父节点锁并创建 Block (初始可能为 Loading)
        using (await this.GetLockForBlock(parentBlockId).LockAsync())
        {
            // 重新检查父节点状态，以防在等待锁期间发生变化
            if (!this.blocks.TryGetValue(parentBlockId, out parentBlockStatus) ||
                parentBlockStatus is not IdleBlockStatus updatedIdleParentBlock)
            {
                Log.Warning($"手动创建 Block 失败: 等待锁后，父 Block '{parentBlockId}' 状态不再是 Idle。");
                return (ManagementResult.InvalidState, null); // 或者重试？取决于策略
            }

            // 克隆状态
            var wsInputClone = updatedIdleParentBlock.Block.wsPostUser?.Clone() ?? updatedIdleParentBlock.Block.wsInput.Clone();
            var gameStateClone = updatedIdleParentBlock.Block.GameState.Clone();

            // 使用标准方式创建 Block (返回 Loading)
            var tempLoadingBlock = Block.CreateBlock(newBlockId, parentBlockId, DEBUG_WorkFlowName, wsInputClone, gameStateClone,
                new Dictionary<string, object?>());

            // 添加到字典 (仍然需要锁新 Block ID，虽然概率极低，但保险起见)
            using (await this.GetLockForBlock(newBlockId).LockAsync())
            {
                if (!this.blocks.TryAdd(newBlockId, tempLoadingBlock))
                {
                    Log.Error($"手动创建 Block 时添加 '{newBlockId}' 失败，已存在同名 Block。");
                    // 理论上极不可能，但如果发生，是严重错误
                    return (ManagementResult.Error, null);
                }

                // 更新父节点的孩子列表
                updatedIdleParentBlock.Block.AddChildren(tempLoadingBlock.Block);
                Log.Debug($"父 Block '{parentBlockId}' 已添加子节点 '{newBlockId}'。");
                // 注意：父节点的变更可能需要持久化（如果使用了数据库）
            }
        } // 释放父节点锁

        // 4. 获取新节点锁并强制转换为 Idle 状态
        IdleBlockStatus finalIdleBlock;
        using (await this.GetLockForBlock(newBlockId).LockAsync())
        {
            // 再次检查状态是否仍是 Loading
            if (!this.blocks.TryGetValue(newBlockId, out var currentStatus) ||
                currentStatus is not LoadingBlockStatus loadingBlock)
            {
                // 可能在释放父锁和获取新锁之间状态被改变了？可能性低但存在
                Log.Error($"尝试将手动创建的 Block '{newBlockId}' 设为 Idle 时发现状态不是 Loading ({currentStatus?.StatusCode})。");
                // 可能需要回滚之前的添加操作？复杂性增加
                return (ManagementResult.Error, null);
            }

            // 执行强制状态转换
            finalIdleBlock = loadingBlock.ForceIdleState();
            this.blocks[newBlockId] = finalIdleBlock; // 更新字典中的状态

            // 应用初始元数据
            if (initialMetadata != null)
            {
                foreach (var kvp in initialMetadata)
                {
                    // 假设 AddOrSetMetaData 是线程安全的或在此锁内安全
                    finalIdleBlock.Block.AddOrSetMetaData(kvp.Key, kvp.Value);
                }
            }

            Log.Info($"手动创建的 Block '{newBlockId}' 已成功设置为 Idle 状态。");
        } // 释放新节点锁

        return (ManagementResult.Success, finalIdleBlock);
    }

    /// <summary>
    /// (内部实现) 手动删除指定的 Block。
    /// </summary>
    public async Task<ManagementResult> InternalDeleteBlockManuallyAsync(string blockId, bool recursive, bool force)
    {
        if (blockId == WorldRootId)
        {
            Log.Warning("尝试手动删除根节点 '__WORLD__'，操作被拒绝。");
            return ManagementResult.CannotPerformOnRoot;
        }

        List<string> blocksToDelete = [blockId];
        List<string> locksToAcquire = []; // 需要获取锁的 Block ID 列表
        string? parentIdToUpdate = null; // 需要更新其 ChildrenList 的父节点

        // 1. 查找需要删除的 Block 及其父节点，并确定是否可以删除
        using (await this.GetLockForBlock(blockId).LockAsync()) // 先锁住目标节点进行检查
        {
            if (!this.blocks.TryGetValue(blockId, out var blockToDeleteStatus))
            {
                Log.Warning($"手动删除 Block 失败: Block '{blockId}' 未找到。");
                return ManagementResult.NotFound;
            }

            parentIdToUpdate = blockToDeleteStatus.Block.ParentBlockId; // 记录父节点ID

            // 检查状态是否允许删除 (除非强制)
            if (!force && !(blockToDeleteStatus is IdleBlockStatus || blockToDeleteStatus is ErrorBlockStatus))
            {
                Log.Warning(
                    $"手动删除 Block 失败: Block '{blockId}' 状态为 {blockToDeleteStatus.StatusCode}，不允许删除（除非 force=true）。");
                return ManagementResult.InvalidState;
            }

            locksToAcquire.Add(blockId); // 确认需要锁定此 Block

            // 如果需要递归删除
            if (recursive)
            {
                var childrenToDelete = this.FindAllDescendants(blockId); // 需要实现 FindAllDescendants
                if (childrenToDelete == null)
                {
                    Log.Error($"手动递归删除 Block '{blockId}' 时查找子孙节点失败。");
                    return ManagementResult.Error; // 查找失败
                }

                blocksToDelete.AddRange(childrenToDelete);
                locksToAcquire.AddRange(childrenToDelete); // 所有子孙都需要锁定
            }
            else if (blockToDeleteStatus.Block.ChildrenList.Count > 0)
            {
                Log.Warning($"手动删除 Block '{blockId}' 失败: Block 有子节点但未指定 recursive=true。");
                return ManagementResult.BadRequest; // 或者定义一个更明确的 Result
            }
        } // 释放目标节点锁

        // 2. 锁定所有相关 Block (父节点和所有要删除的节点)
        // 注意：获取多个锁需要小心死锁。按 ID 排序获取可以避免简单死锁。
        var sortedLockIds = locksToAcquire
            .Union(parentIdToUpdate != null ? [parentIdToUpdate] : [])
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var acquiredLocks = new List<IDisposable>();
        try
        {
            Log.Debug($"准备获取删除操作所需的锁: {string.Join(", ", sortedLockIds)}");
            foreach (var lockId in sortedLockIds)
            {
                acquiredLocks.Add(await this.GetLockForBlock(lockId).LockAsync());
            }

            Log.Debug("删除操作所需锁已全部获取。");

            // 3. 在锁保护下执行删除
            // 3.1 再次检查父节点是否存在 (以防在等待锁时被删除)
            BlockStatus? parentStatus = null;
            if (parentIdToUpdate != null && !this.blocks.TryGetValue(parentIdToUpdate, out parentStatus))
            {
                Log.Warning($"手动删除 Block '{blockId}' 失败: 其父节点 '{parentIdToUpdate}' 在获取锁后消失。");
                return ManagementResult.NotFound; // 或 Error?
            }

            // 3.2 移除 Block 字典中的条目
            int removedCount = 0;
            foreach (var idToRemove in blocksToDelete)
            {
                if (this.blocks.TryRemove(idToRemove, out _))
                {
                    this.blockLocks.TryRemove(idToRemove, out _); // 同时移除锁对象，避免内存泄漏
                    removedCount++;
                    Log.Debug($"Block '{idToRemove}' 已从字典中移除。");
                }
                else
                {
                    Log.Warning($"尝试删除 Block '{idToRemove}' 时发现它已不在字典中。");
                }
            }

            // 3.3 从父节点的 ChildrenList 中移除
            if (parentIdToUpdate != null && parentStatus != null)
            {
                if (parentStatus.Block.ChildrenList.Remove(blockId))
                {
                    Log.Debug($"Block '{blockId}' 已从父节点 '{parentIdToUpdate}' 的子列表中移除。");
                    // 可能需要持久化父节点变更
                }
                else
                {
                    Log.Warning($"尝试从父节点 '{parentIdToUpdate}' 移除子节点 '{blockId}' 时发现它不存在于列表中。");
                }
            }

            Log.Info($"手动删除操作完成。共移除 {removedCount} 个 Block (请求删除 {blocksToDelete.Count} 个)。");
            return ManagementResult.Success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "手动删除 Block 时发生意外错误。");
            return ManagementResult.Error;
        }
        finally
        {
            // 确保所有锁都被释放
            foreach (var lck in acquiredLocks)
            {
                lck.Dispose();
            }

            Log.Debug("删除操作的锁已释放。");
        }
    }

    /// <summary>
    /// (内部实现) 手动移动指定的 Block 到新的父节点下。
    /// </summary>
    public async Task<ManagementResult> InternalMoveBlockManuallyAsync(string blockId, string newParentBlockId)
    {
        if (blockId == WorldRootId) return ManagementResult.CannotPerformOnRoot;
        if (blockId == newParentBlockId) return ManagementResult.CyclicOperation;

        string? oldParentId;

        // 1. 预检查和信息收集 (只读，获取少量锁)
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var blockToMoveStatus)) return ManagementResult.NotFound;
            oldParentId = blockToMoveStatus.Block.ParentBlockId;

            // 检查状态是否允许移动 (例如，不能移动 Loading/Resolving?) - 根据需要添加
            // if (!force && blockToMoveStatus is LoadingBlockStatus or ConflictBlockStatus) return ManagementResult.InvalidState;

            // 查找所有子孙，检查循环引用
            var descendantIds = this.FindAllDescendants(blockId);
            if (descendantIds == null) return ManagementResult.Error; // 查找失败
            if (descendantIds.Contains(newParentBlockId)) return ManagementResult.CyclicOperation;
        }

        if (oldParentId == newParentBlockId) return ManagementResult.Success; // 没必要移动

        using (await this.GetLockForBlock(newParentBlockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(newParentBlockId, out var newParentStatus)) return ManagementResult.NotFound;
            // 检查新父节点状态是否允许接收子节点 (e.g., Idle?)
            if (newParentStatus is not IdleBlockStatus) return ManagementResult.InvalidState;
        }

        if (oldParentId == null)
        {
            // 尝试移动一个直接在根下的 Block (旧父节点是 __WORLD__?)
            // 这种情况通常是允许的，但需要确认 oldParentId == WorldRootId
            // 如果 oldParentId 真的是 null (数据错误?)，则可能需要特殊处理或报错
            Log.Warning($"Block '{blockId}' 的 ParentId 为 null，但它不是根节点。数据可能存在问题。");
            // 根据策略决定是否继续或返回错误
        }


        // 2. 锁定所有相关 Block (要移动的, 旧父, 新父)
        var lockIds = new List<string> { blockId, newParentBlockId };
        if (oldParentId != null) lockIds.Add(oldParentId);
        var sortedLockIds = lockIds.Distinct().OrderBy(id => id).ToList();

        var acquiredLocks = new List<IDisposable>();
        try
        {
            Log.Debug($"准备获取移动操作所需的锁: {string.Join(", ", sortedLockIds)}");
            foreach (var lockId in sortedLockIds)
            {
                acquiredLocks.Add(await this.GetLockForBlock(lockId).LockAsync());
            }

            Log.Debug("移动操作所需锁已全部获取。");

            // 3. 在锁保护下执行移动
            // 3.1 重新获取并验证所有相关 Block 的状态
            if (!this.blocks.TryGetValue(blockId, out var blockToMove) ||
                !this.blocks.TryGetValue(newParentBlockId, out var newParent) ||
                (oldParentId != null && !this.blocks.TryGetValue(oldParentId, out var oldParent)))
            {
                Log.Warning("移动操作失败：一个或多个相关 Block 在获取锁后消失。");
                return ManagementResult.NotFound; // 或者 Error
            }

            // 再次检查状态 (可选，但更安全)
            if (newParent is not IdleBlockStatus) return ManagementResult.InvalidState;
            // if (!force && blockToMove is LoadingBlockStatus or ConflictBlockStatus) return ManagementResult.InvalidState;

            // 3.2 从旧父节点移除 (如果旧父存在且不是根)
            if (oldParentId != null &&
                this.blocks.TryGetValue(oldParentId, out var oldParentStatus)) // 确保 oldParent 变量已更新
            {
                if (!oldParentStatus.Block.ChildrenList.Remove(blockId))
                {
                    Log.Warning($"尝试从旧父节点 '{oldParentId}' 移除子节点 '{blockId}' 时未找到。数据可能不一致。");
                    // 可能需要决定是否继续
                }
                else
                {
                    Log.Debug($"Block '{blockId}' 已从旧父节点 '{oldParentId}' 的子列表中移除。");
                    // 持久化旧父节点变更
                }
            }

            // 3.3 更新 Block 的 ParentBlockId
            // Block 是 class，可以直接修改。如果是 record struct，需要创建新实例替换。
            // 假设 Block 是 class:
            blockToMove.Block.GetType().GetProperty("ParentBlockId")
                ?.SetValue(blockToMove.Block, newParentBlockId); // 使用反射或提供 internal setter
            Log.Debug($"Block '{blockId}' 的 ParentBlockId 已更新为 '{newParentBlockId}'。");
            // 持久化 blockToMove 的变更

            // 3.4 添加到新父节点的 ChildrenList
            newParent.Block.AddChildren(blockToMove.Block); // AddChildren 应该处理重复添加 (虽然理论上不应发生)
            Log.Debug($"Block '{blockId}' 已添加到新父节点 '{newParentBlockId}' 的子列表中。");
            // 持久化新父节点变更

            Log.Info($"Block '{blockId}' 已成功移动到父节点 '{newParentBlockId}' 下。");
            return ManagementResult.Success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"手动移动 Block '{blockId}' 时发生意外错误。");
            return ManagementResult.Error;
        }
        finally
        {
            foreach (var lck in acquiredLocks) lck.Dispose();
            Log.Debug("移动操作的锁已释放。");
        }
    }

    /// <summary>
    /// 辅助方法：查找指定 Block ID 的所有后代 Block ID。
    /// 返回 null 表示查找过程中出错。
    /// </summary>
    private List<string>? FindAllDescendants(string startBlockId)
    {
        var descendants = new List<string>();
        var queue = new Queue<string>();

        // 先获取起始节点的子节点
        if (!this.blocks.TryGetValue(startBlockId, out var startBlockStatus))
        {
            Log.Error($"FindAllDescendants 错误：起始节点 '{startBlockId}' 未找到。");
            return null; // 起始节点不存在
        }

        foreach (var childId in startBlockStatus.Block.ChildrenList)
        {
            queue.Enqueue(childId);
        }

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (descendants.Contains(currentId)) continue; // 避免循环引用导致的无限循环

            if (!this.blocks.TryGetValue(currentId, out var currentBlockStatus))
            {
                Log.Warning($"FindAllDescendants 警告：子孙节点 '{currentId}' 在字典中未找到，可能数据不一致。");
                continue; // 跳过不存在的节点
            }

            descendants.Add(currentId);

            foreach (var childId in currentBlockStatus.Block.ChildrenList)
            {
                queue.Enqueue(childId);
            }
        }

        return descendants;
    }

    #endregion
}

public record BlockStatusError(BlockResultCode Code, string Message, BlockStatus? FailedBlockStatus) : LazyInitError(Message)
{
    public static BlockStatusError NotFound(BlockStatus? block, string message)
        => new(BlockResultCode.NotFound, message, block);

    public static BlockStatusError Conflict(BlockStatus block, string message)
        => new(BlockResultCode.Conflict, message, block);

    public static BlockStatusError InvalidInput(BlockStatus block, string message)
        => new(BlockResultCode.InvalidInput, message, block);

    public static BlockStatusError Error(BlockStatus block, string message)
        => new(BlockResultCode.Error, message, block);

    public static implicit operator Result(BlockStatusError initError) => initError.ToResult();
}