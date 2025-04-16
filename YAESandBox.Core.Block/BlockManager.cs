using System.Collections.Concurrent;
using Nito.AsyncEx;
using OneOf;
using OneOf.Types;
using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Depend;

namespace YAESandBox.Core.Block;

public class BlockManager
{
    /// <summary>
    /// 所有根节点的父节点，一个超级根节点。
    /// </summary>
    private const string WorldRootId = "__WORLD__";

    public BlockManager()
    {
        if (this.blocks.TryAdd(WorldRootId, Block.CreateBlock(WorldRootId, null, new WorldState(), new GameState())))
            Log.Info("BlockManager: 根节点已创建。");
        else
            throw new Exception($"添加默认根 Block '{WorldRootId}' 失败。");
    }

    private ConcurrentDictionary<string, BlockStatus> blocks { get; } = new();
    private ConcurrentDictionary<string, AsyncLock> blockLocks { get; } = new(); // 并发控制

    private AsyncLock GetLockForBlock(string blockId)
    {
        return this.blockLocks.GetOrAdd(blockId, _ => new AsyncLock());
    }

    /// <summary>
    /// 创建子Block，需要父BlockId和触发参数
    /// </summary>
    /// <param name="parentBlockId"></param>
    /// <param name="triggerParams"></param>
    /// <returns></returns>
    public async Task<BlockStatus?> CreateChildBlock_Async(
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
                int childIndex = parentBlock.block.ChildrenInfo.Count;
                parentBlock.block.ChildrenInfo[childIndex] = newBlockId;
                parentBlock.block.TriggeredChildParams = triggerParams;
                parentBlock.block.SelectedChildIndex = childIndex; // Select the new child
                Log.Debug($"父 Block '{parentBlockId}': 已添加子 Block '{newBlockId}' 记录，索引为 {childIndex}。");
                // TODO: 持久化 Parent Block changes

                Log.Info($"新 Block '{newBlockId}' 已创建并添加，状态: {newChildBlock.GetType()}。");
                // TODO: 持久化 New Block
            }

            return newChildBlock;
        }
    }

    // public async Task<LoadingBlockStatus?> CreateRootBlock_Async(Dictionary<string, object?> triggerParams)
    // {
    //     const string rootId = "blc_root_block";
    //     var newBlock = Block.CreateBlock(rootId, null, new WorldState(), new GameState());
    //     //TODO 还没有加入队列和设置根节点（问题：理论上可以有多个根节点（兄弟节点），所以似乎我现在这么做有问题，应该Manager创建时自带一个隐式的根节点？或者维护一个根列表？）
    // }

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

            return conflictBlock.FinalizeConflictResolution(block.block.BlockContent, resolvedCommands);
        }
    }

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
}