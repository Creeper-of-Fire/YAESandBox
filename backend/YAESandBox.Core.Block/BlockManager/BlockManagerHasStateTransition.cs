using FluentResults;
using YAESandBox.Core.Action;
using YAESandBox.Depend;
using OneOf;

namespace YAESandBox.Core.Block;

// BlockManager中，可能导致Block状态转换的那部分代码。
public partial class BlockManager
{
    internal void TrySetBlock(BlockStatus? blockStatus)
    {
        if (blockStatus == null) return;
        this.blocks.AddOrUpdate(blockStatus.Block.BlockId, _ => blockStatus, (_, _) => blockStatus);
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
    public async Task<Result<IEnumerable<AtomicOperation>>>
        ApplyResolvedCommandsAsync(string blockId, List<AtomicOperation> resolvedCommands)
    {
        // This logic is now mostly inside HandleWorkflowCompletionAsync after conflict resolution.
        // This method might be used if we implement manual conflict resolution flow.
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var block))
            {
                return BlockStatusError.NotFound(null, $"尝试应用已解决指令失败: Block '{blockId}' 未找到。").ToResult();
            }

            if (block is not ConflictBlockStatus conflictBlock)
            {
                return NormalHandledIssue.InvalidState(
                    $"尝试应用已解决指令，但 Block '{blockId}' 状态为 {block.StatusCode} (非 ResolvingConflict)。已忽略。").ToResult();
            }

            Log.Info($"Block '{blockId}': 正在应用手动解决的冲突指令 ({resolvedCommands.Count} 条)。");

            var val = conflictBlock.FinalizeConflictResolution(block.Block.BlockContent, resolvedCommands);
            this.TrySetBlock(val.block);
            return val.atomicOp;
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
    public async Task<OneOf<(IdleBlockStatus, Result<IEnumerable<AtomicOperation>>), ConflictBlockStatus, ErrorBlockStatus, IReason>>
        HandleWorkflowCompletionAsync(string blockId, bool success, string rawText,
            List<AtomicOperation> firstPartyCommands, Dictionary<string, object?> outputVariables)
    {
        using (await this.GetLockForBlock(blockId).LockAsync())
        {
            if (!this.blocks.TryGetValue(blockId, out var blockStatus))
            {
                return BlockStatusError.NotFound(null, $"处理工作流完成失败: Block '{blockId}' 未找到。");
            }

            if (blockStatus is not LoadingBlockStatus block)
            {
                return NormalHandledIssue.InvalidState($"收到 Block '{blockId}' 的工作流完成回调，但其状态为 {blockStatus.StatusCode} (非 Loading)。可能重复或过时。");
            }

            if (!success) // Workflow failed
            {
                Log.Error($"Block '{blockId}': 工作流执行失败。已转为错误状态。");
                var errorStatus = block.toErrorStatus();
                this.blocks[blockId] = errorStatus;
                return errorStatus;
            }

            // Store output variables in metadata?
            block.Block.AddOrSetMetaData("WorkflowOutputVariables", outputVariables);

            Log.Info($"Block '{blockId}': 工作流成功完成。准备处理指令和状态。");
            var val = block.TryFinalizeSuccessfulWorkflow(rawText, firstPartyCommands);
            val.Switch(tuple => this.TrySetBlock(tuple.blockStatus), this.TrySetBlock);
            return val.Match<OneOf<(IdleBlockStatus, Result<IEnumerable<AtomicOperation>>), ConflictBlockStatus, ErrorBlockStatus, IReason>>(
                tuple => tuple,
                conflict => conflict);
        }
    }
}