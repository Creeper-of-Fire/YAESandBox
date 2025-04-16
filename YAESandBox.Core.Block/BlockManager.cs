using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nito.AsyncEx;
using OneOf;
using OneOf.Types;
using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;

namespace YAESandBox.Core.Block;

public class BlockManager
{
    /// <summary>
    /// 构造函数，创建默认根节点。
    /// </summary>
    /// <exception cref="Exception"></exception>
    public BlockManager()
    {
        if (this.blocks.TryAdd(WorldRootId, Block.CreateBlock(WorldRootId, null, new WorldState(), new GameState())))
            Log.Info("BlockManager: 根节点已创建。");
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

    /// <summary>
    /// 全局锁，用于控制对单个 BlockManager 的并发访问。
    /// </summary>
    private AsyncLock globalLoadLock { get; } = new AsyncLock();

    public IReadOnlyDictionary<string, Block> Blocks => this.blocks.ToDictionary(kv => kv.Key, kv => kv.Value.Block);

    public IReadOnlyDictionary<string, IBlockNode> NodeOnlyBlocks =>
        this.blocks.ToDictionary(kv => kv.Key, IBlockNode (kv) => kv.Value.Block);

    /// <summary>
    /// 创建子Block，需要父BlockId和触发参数
    /// </summary>
    /// <param name="parentBlockId"></param>
    /// <param name="triggerParams"></param>
    /// <returns></returns>
    public async Task<LoadingBlockStatus?> CreateChildBlock_Async(
        string? parentBlockId, Dictionary<string, object?> triggerParams)
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

            (string newBlockId, var newChildBlock) = idleParentBlock.CreateNewChildrenBlock();


            // 4. Add New Block to Manager's Dictionary (Lock the *new* block ID)
            using (await this.GetLockForBlock(newBlockId).LockAsync())
            {
                if (!this.blocks.TryAdd(newBlockId, newChildBlock))
                {
                    Log.Error($"添加新 Block '{newBlockId}' 失败，可能已存在同名 Block。这个概率是地球沙子数量的平方，恭喜你中大奖了！（也有可能是随机数生成器坏了？）");
                    return null;
                }

                // 3. Add Child Info to Parent
                parentBlock.Block.TriggeredChildParams = triggerParams;
                parentBlock.Block.AddChildren(newChildBlock.Block);
                Log.Debug($"父 Block '{parentBlockId}': 已添加子 Block '{newBlockId}' 记录。");
                // TODO: 持久化 Parent Block changes

                Log.Info($"新 Block '{newBlockId}' 已创建并添加，状态: {newChildBlock.GetType()}。");
                // TODO: 持久化 New Block
            }

            return newChildBlock;
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
            if (currentBlock.Block.ChildrenList.Any())
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
    /// 处理已解决冲突的指令。
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="resolvedCommands"></param>
    /// <returns></returns>
    public async Task<(OneOf<IdleBlockStatus, ErrorBlockStatus>? blockStatus, List<OperationResult>? results)>
        ApplyResolvedCommandsAsync(string blockId, List<AtomicOperation> resolvedCommands)
    {
        // This logic is now mostly inside HandleWorkflowCompletionAsync after conflict resolution.
        // This method might be used if we implement manual conflict resolution flow.
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
            {
                Log.Error($"尝试应用已解决指令失败: Block '{blockId}' 未找到。");
                return (null, null);
            }

            if (block is not ConflictBlockStatus conflictBlock)
            {
                Log.Warning($"尝试应用已解决指令，但 Block '{blockId}' 状态为 {block.StatusCode} (非 ResolvingConflict)。已忽略。");
                return (null, null);
            }

            Log.Info($"Block '{blockId}': 正在应用手动解决的冲突指令 ({resolvedCommands.Count} 条)。");

            return conflictBlock.FinalizeConflictResolution(block.Block.BlockContent, resolvedCommands);
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
    public async Task<(OneOf<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus>? blockStatus,
            List<OperationResult>? results)>
        EnqueueOrExecuteAtomicOperationsAsync(string blockId, List<AtomicOperation> operations)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
            {
                Log.Error($"尝试执行原子操作失败: Block '{blockId}' 未找到。");
                return (null, null);
            }

            switch (block)
            {
                case LoadingBlockStatus loadingBlock:
                    return (loadingBlock, loadingBlock.ApplyOperations(operations));
                case IdleBlockStatus idleBlock:
                    return (idleBlock, idleBlock.ApplyOperations(operations));
                case ConflictBlockStatus conflictBlock:
                    return (conflictBlock, null);
                case ErrorBlockStatus errorBlock:
                    return (errorBlock, null);
                default:
                    Log.Error($"尝试执行原子操作失败: Block '{blockId}' 状态为 {block.StatusCode}。");
                    return (null, null);
            }
        }
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
    /// 
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="success"></param>
    /// <param name="rawText"></param>
    /// <param name="firstPartyCommands">来自第一公民工作流的指令</param>
    /// <param name="outputVariables"></param>
    public async
        Task<OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results),
            ConflictBlockStatus, ErrorBlockStatus>?>
        HandleWorkflowCompletionAsync(string blockId, bool success, string rawText,
            List<AtomicOperation> firstPartyCommands, Dictionary<string, object?> outputVariables)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var blockStatus))
            {
                Log.Error($"处理工作流完成失败: Block '{blockId}' 未找到。");
                return null;
            }

            if (blockStatus is not LoadingBlockStatus block)
            {
                Log.Warning($"收到 Block '{blockId}' 的工作流完成回调，但其状态为 {blockStatus.StatusCode} (非 Loading)。可能重复或过时。");
                return null;
            }

            if (!success) // Workflow failed
            {
                Log.Error($"Block '{blockId}': 工作流执行失败。已转为错误状态。");
                var errorStatus = block.toErrorStatus();
                this.blocks[blockId] = errorStatus;
                return errorStatus;
            }

            // Store output variables in metadata?
            block.Block.Metadata["WorkflowOutputVariables"] = outputVariables;

            Log.Info($"Block '{blockId}': 工作流成功完成。准备处理指令和状态。");
            var val = block.TryFinalizeSuccessfulWorkflow(rawText, firstPartyCommands);
            return val.Widen<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results),
                ConflictBlockStatus, ErrorBlockStatus>();
        }
    }

    //--- 持久化逻辑 --- 


    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true, // For readability
        Converters = { new JsonStringEnumConverter(), new TypedIdConverter() }, // 注册转换器
        // PropertyNameCaseInsensitive = true // Optional for loading flexibility
    };

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
                blockDto.WorldStates["wsPostAI"] = MapWorldStateDto(coreBlock.wsPostAI);
            if (coreBlock.wsPostUser != null)
                blockDto.WorldStates["wsPostUser"] = MapWorldStateDto(coreBlock.wsPostUser);
            // wsTemp 不保存

            archive.Blocks.Add(blockId, blockDto);
        }

        await JsonSerializer.SerializeAsync(stream, archive, _jsonOptions);
        Log.Info($"BlockManager state saved. Blocks count: {archive.Blocks.Count}");
    }

    /// <summary>
    /// 从流中加载 BlockManager 的状态。
    /// </summary>
    /// <param name="stream">要读取的流。</param>
    /// <returns>恢复的前端盲存数据。</returns>
    public async Task<object?> LoadFromFileAsync(Stream stream)
    {
        var archive = await JsonSerializer.DeserializeAsync<ArchiveDto>(stream, _jsonOptions);

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
                    Block.CreateBlock(WorldRootId, null, new WorldState(), new GameState()));
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
            var metadata = blockDto.Metadata.ToDictionary(entry => entry.Key,
                entry => PersistenceMapper.DeserializeObjectValue(entry.Value));
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
        MapEntitiesDto(dto.Items, ws.Items);
        MapEntitiesDto(dto.Characters, ws.Characters);
        MapEntitiesDto(dto.Places, ws.Places);
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
}