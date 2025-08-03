using System.Globalization;
using OneOf;
using YAESandBox.Seed.Action;
using YAESandBox.Depend;
using YAESandBox.Depend.Results;

// ReSharper disable ParameterTypeCanBeEnumerable.Global
namespace YAESandBox.Seed.Block.BlockManager;

// BlockManager中，可能导致Block状态转换的那部分代码。
public partial class BlockManager
{
    internal void TrySetBlock(BlockStatus? blockStatus)
    {
        if (blockStatus == null) return;
        this.Blocks.AddOrUpdate(blockStatus.Block.BlockId, _ => blockStatus, (_, _) => blockStatus);
    }

    internal void TrySetBlock<T0, T1>(OneOf<T0, T1> blockStatus)
        where T0 : BlockStatus
        where T1 : BlockStatus
    {
        this.TrySetBlock(blockStatus.Match<BlockStatus?>(t0 => t0, t1 => t1));
    }

    internal void TrySetBlock<T0, T1, T2>(OneOf<T0, T1, T2> blockStatus)
        where T0 : BlockStatus
        where T1 : BlockStatus
        where T2 : BlockStatus
    {
        this.TrySetBlock(blockStatus.Match<BlockStatus?>(t0 => t0, t1 => t1, t2 => t2));
    }

    internal void TrySetBlock<T0, T1, T2, T3>(OneOf<T0, T1, T2, T3> blockStatus)
        where T0 : BlockStatus
        where T1 : BlockStatus
        where T2 : BlockStatus
        where T3 : BlockStatus
    {
        this.TrySetBlock(blockStatus.Match<BlockStatus?>(t0 => t0, t1 => t1, t2 => t2, t3 => t3));
    }

    /// <summary>
    /// 处理已解决冲突的指令。
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="resolvedCommands"></param>
    /// <returns></returns>
    [HasBlockStateTransition]
    public async Task<CollectionResult<AtomicOperation>> ApplyResolvedCommandsAsync(string blockId,
        IReadOnlyList<AtomicOperation> resolvedCommands)
    {
        // This logic is now mostly inside HandleWorkflowCompletionAsync after conflict resolution.
        // This method might be used if we implement manual conflict resolution flow.
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.Blocks.TryGetValue(blockId, out var block))
                return BlockStatusError.NotFound(null, $"尝试应用已解决指令失败: Block '{blockId}' 未找到。")
                    .ToCollectionResult<AtomicOperation>();

            if (block is not ConflictBlockStatus conflictBlock)
                return BlockStatusError
                    .InvalidState(block, $"尝试应用已解决指令，但 Block '{blockId}' 状态为 {block.StatusCode} (非 ResolvingConflict)。已忽略。")
                    .ToCollectionResult<AtomicOperation>();

            Log.Info($"Block '{blockId}': 正在应用手动解决的冲突指令 ({resolvedCommands.Count} 条)。");

            var val = conflictBlock.FinalizeConflictResolution(block.Block.BlockContent, resolvedCommands);
            this.TrySetBlock(val.block);
            return CollectionResult<AtomicOperation>.Ok(val.atomicOp);
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
    [HasBlockStateTransition]
    public async Task<OneOf<(IdleBlockStatus, CollectionResult<AtomicOperation>), ConflictBlockStatus, ErrorBlockStatus, Error>>
        HandleWorkflowCompletionAsync(string blockId, bool success, string rawText,
            IReadOnlyList<AtomicOperation> firstPartyCommands, IReadOnlyDictionary<string, object?> outputVariables)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.Blocks.TryGetValue(blockId, out var blockStatus))
                return BlockStatusError.NotFound(null, $"处理工作流完成失败: Block '{blockId}' 未找到。");

            if (blockStatus is not LoadingBlockStatus block)
                return BlockStatusError.InvalidState(blockStatus,
                    $"收到 Block '{blockId}' 的工作流完成回调，但其状态为 {blockStatus.StatusCode} (非 Loading)。可能重复或过时。");

            if (!success) // Workflow failed
            {
                Log.Error($"Block '{blockId}': 工作流执行失败。已转为错误状态。");
                var errorStatus = block.ToErrorStatus();
                this.Blocks[blockId] = errorStatus;
                return errorStatus;
            }

            // Store output variables in metadata?
            block.Block.AddOrSetMetaData("WorkflowOutputVariables", outputVariables);

            Log.Info($"Block '{blockId}': 工作流成功完成。准备处理指令和状态。");
            var val = block.TryFinalizeSuccessfulWorkflow(rawText, firstPartyCommands);
            val.Switch(tuple => this.TrySetBlock(tuple.blockStatus), this.TrySetBlock);
            return val
                .Match<OneOf<(IdleBlockStatus, CollectionResult<AtomicOperation>), ConflictBlockStatus, ErrorBlockStatus, Error>>(
                    tuple => (tuple.blockStatus, CollectionResult<AtomicOperation>.Ok(tuple.atomicOp)),
                    conflict => conflict);
        }
    }

    /// <inheritdoc/>
    [HasBlockStateTransition]
    public async Task<Result<LoadingBlockStatus>> StartRegenerationAsync(string blockId)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.Blocks.TryGetValue(blockId, out var blockStatus))
            {
                Log.Error($"BlockManager: 尝试为 Block '{blockId}' 启动重新生成失败：Block 未找到。");
                return BlockStatusError.NotFound(null, $"尝试为 Block '{blockId}' 启动重新生成失败：Block 未找到。");
            }

            var sourceWsForRegen = blockStatus.Block.WsInput;
            var coreBlock = blockStatus.Block; // 获取核心 Block 对象

            // --- 准备 Block 以进入 Loading 状态 ---
            Log.Debug($"BlockManager: 准备将 Block '{blockId}' 从 {blockStatus.StatusCode} 转为 Loading 状态...");

            // 1. 清理旧的输出和临时状态
            coreBlock.WsPostAi = null;
            coreBlock.WsPostUser = null;
            coreBlock.WsTemp = null;

            // 2. 创建新的 wsTemp (基于选定的源)
            coreBlock.WsTemp = sourceWsForRegen.Clone();
            Log.Debug($"BlockManager: Block '{blockId}': wsTemp 已基于源 WorldState 重新克隆。");


            // 3. 更新触发参数和元数据
            coreBlock.AddOrSetMetaData("RegenerationStartTime", DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
            // 可以考虑移除旧的 WorkflowOutputVariables 或其他相关元数据
            coreBlock.RemoveMetaData("WorkflowOutputVariables"); // 示例：移除旧输出

            // 4. 创建新的 LoadingBlockStatus 实例
            var newLoadingStatus = new LoadingBlockStatus(coreBlock);

            // 5. 更新 BlockManager 中的状态字典
            // 使用 AddOrUpdate 确保线程安全地替换旧状态
            this.Blocks.AddOrUpdate(blockId, newLoadingStatus, (key, oldStatus) => newLoadingStatus);

            Log.Info($"BlockManager: Block '{blockId}' 已成功转换到 Loading 状态，准备重新生成。");
            return newLoadingStatus;
        }
    }
}