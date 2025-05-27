using System.Collections.Concurrent;
using FluentResults;
using Nito.AsyncEx;
using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Depend;
using YAESandBox.Depend.Results;

// ReSharper disable ParameterTypeCanBeEnumerable.Global
namespace YAESandBox.Core.Block.BlockManager;

public partial class BlockManager : IBlockManager
{
    private const string DebugWorkFlowName = "";

    /// <summary>
    /// 构造函数，创建默认根节点。
    /// </summary>
    /// <exception cref="Exception"></exception>
    public BlockManager()
    {
        var rootBlock = Block.CreateBlock(WorldRootId, null, DebugWorkFlowName, new WorldState(), new GameState());
        if (this.Blocks.TryAdd(WorldRootId, rootBlock.ForceIdleState()))
            Log.Info("BlockManager: 根节点已创建并设置为空闲。");
        else
            throw new Exception($"添加默认根 Block '{WorldRootId}' 失败。");
    }

    /// <summary>
    /// 所有根节点的父节点，一个超级根节点。如果创建节点时没有提供父节点，则自动创建到它下面。
    /// </summary>
    public const string WorldRootId = "__WORLD__";

    private ConcurrentDictionary<string, BlockStatus> Blocks { get; } = new();
    private ConcurrentDictionary<string, AsyncLock> BlockLocks { get; } = new(); // 并发控制

    /// <summary>
    /// 用于控制对单个 Block 的并发访问。
    /// </summary>
    /// <returns></returns>
    private AsyncLock GetLockForBlock(string blockId)
    {
        return this.BlockLocks.GetOrAdd(blockId, _ => new AsyncLock());
    }

    public IReadOnlyDictionary<string, Block> GetBlocks()
    {
        return this.Blocks.ToDictionary(kv => kv.Key, kv => kv.Value.Block);
    }

    public IReadOnlyDictionary<string, IBlockNode> GetNodeOnlyBlocks()
    {
        return this.Blocks.ToDictionary(kv => kv.Key, IBlockNode (kv) => kv.Value.Block);
    }

    /// <summary>
    /// 创建子Block，需要父BlockId和触发参数
    /// </summary>
    /// <param name="parentBlockId"></param>
    /// <param name="workFlowName"></param>
    /// <param name="triggerParams"></param>
    /// <returns></returns>
    public async Task<LoadingBlockStatus?> CreateChildBlock_Async(
        string? parentBlockId, string workFlowName, IReadOnlyDictionary<string, string> triggerParams)
    {
        parentBlockId ??= WorldRootId;
        using (await this.GetLockForBlock(parentBlockId).LockAsync()) // Lock parent to add child info
        {
            // 1. Get Parent Block
            if (!this.Blocks.TryGetValue(parentBlockId, out var parentBlock))
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

            parentBlock.Block.TriggeredChildParams = triggerParams.ToDictionary();

            (string newBlockId, var newChildBlock) = idleParentBlock.CreateNewChildrenBlock(workFlowName);


            // 4. Add New Block to Manager's Dictionary (Lock the *new* block ID)
            using (await this.GetLockForBlock(newBlockId).LockAsync())
            {
                if (!this.Blocks.TryAdd(newBlockId, newChildBlock))
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

    /// <summary>
    /// 更新指定 Block 的内容和/或元数据。
    /// 仅在 Block 处于 Idle 状态时允许操作。
    /// </summary>
    /// <param name="blockId">要更新的 Block ID。</param>
    /// <param name="newContent">新的 Block 内容。如果为 null 则不更新。</param>
    /// <param name="metadataUpdates">要更新或移除的元数据。Key 为元数据键，Value 为新值（null 表示移除）。如果为 null 则不更新。</param>
    /// <returns>更新操作的结果。</returns>
    public async Task<BlockResultCode> UpdateBlockDetailsAsync(string blockId, string? newContent,
        IReadOnlyDictionary<string, string?>? metadataUpdates)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.Blocks.TryGetValue(blockId, out var blockStatus)) return BlockResultCode.NotFound;

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
                Log.Info($"Block '{blockId}': 内容或元数据已成功更新。");
            // 可以在这里添加持久化逻辑，如果需要立即保存这些更改
            // await PersistBlockAsync(idleBlock.Block);
            // 决定是否发送通知。目前我们不发送 SignalR 通知。
            // 如果需要，可以在此调用 notifierService.NotifyStateUpdateSignal(blockId);
            else
                Log.Debug($"Block '{blockId}': 收到更新请求，但没有提供有效的更新内容或元数据。");

            return BlockResultCode.Success;
        }
    }

    /// <summary>
    /// 获取从根节点到指定块ID可达的最深层叶子节点（根据“最后一个子节点”规则）的完整路径。
    /// 该方法首先确定从起始块向下到最深叶子的路径，然后反向追溯到根节点。
    /// </summary>
    /// <param name="startBlockId">起始块的ID。假定此ID在 'blocks' 字典中有效。</param>
    /// <returns>一个包含从根节点到最深层叶子节点ID的列表。如果路径中遇到数据不一致（如引用了不存在的块），则记录错误并返回空列表。</returns>
    public IReadOnlyList<string> GetPathToRoot(string startBlockId)
    {
        // --- 阶段 1: 查找从 startBlockId 出发，遵循“最后一个子节点”规则到达的最深叶节点 ---
        string currentId = startBlockId; // 当前遍历的节点ID

        // 循环向下查找，直到遇到没有子节点的块 或 遇到数据不一致的情况
        while (this.Blocks.TryGetValue(currentId, out var currentBlock))
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
        if (!this.Blocks.ContainsKey(currentId))
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
            if (!this.Blocks.TryGetValue(idToTrace, out var blockData))
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
        this.Blocks.TryGetValue(blockId, out var block);
        if (block == null)
            Log.Warning(
                $"GetBlockAsync: 未在 _blocks 字典中找到 Block ID: '{blockId}'。当前字典大小: {this.Blocks.Count}");
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
    public async Task<BlockResultCode> UpdateBlockGameStateAsync(string blockId, IReadOnlyDictionary<string, object?> settingsToUpdate)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.Blocks.TryGetValue(blockId, out var block))
                return BlockResultCode.NotFound;

            foreach (var kvp in settingsToUpdate)
                block.Block.GameState[kvp.Key] = kvp.Value;

            Log.Debug($"Block '{blockId}': GameState 已更新。");
            return BlockResultCode.Success;
        }
    }

    /// <summary>
    /// 异步执行或排队原子操作。
    /// 如果为IdleBlockStatus和LoadingBlockStatus，则执行（并在 Loading 期间排队）。
    /// 如果为ConflictBlockStatus或ErrorBlockStatus，则不执行任何东西。
    /// </summary>
    /// <param name="blockId">区块唯一标识符</param>
    /// <param name="operations">待执行的原子操作列表</param>
    /// <returns>返回一个元组，包含区块状态和操作结果列表（结果包含失败的）</returns>
    public async Task<(Result<IReadOnlyList<AtomicOperation>> result, BlockStatusCode? blockStatusCode)>
        EnqueueOrExecuteAtomicOperationsAsync(string blockId, IReadOnlyList<AtomicOperation> operations)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.Blocks.TryGetValue(blockId, out var block))
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
}

public record BlockStatusError(BlockResultCode Code, string Message, BlockStatus? FailedBlockStatus) : LazyInitError(Message)
{
    public static BlockStatusError NotFound(BlockStatus? block, string message)
    {
        return new BlockStatusError(BlockResultCode.NotFound, message, block);
    }

    public static BlockStatusError Conflict(BlockStatus block, string message)
    {
        return new BlockStatusError(BlockResultCode.Conflict, message, block);
    }

    public static BlockStatusError InvalidInput(BlockStatus block, string message)
    {
        return new BlockStatusError(BlockResultCode.InvalidInput, message, block);
    }

    public static BlockStatusError Error(BlockStatus block, string message)
    {
        return new BlockStatusError(BlockResultCode.Error, message, block);
    }

    public static implicit operator Result(BlockStatusError initError)
    {
        return initError.ToResult();
    }
}