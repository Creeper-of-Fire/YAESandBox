using FluentResults;
using YAESandBox.API.DTOs;
using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.Core.Action;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Depend;

namespace YAESandBox.API.Services;

/// <summary>
/// BlockWritService 用于管理 Block 的链接和交互。
/// 同时也提供输入原子化指令修改其内容的服务（修改Block数据的唯一入口），之后可能分离。
/// </summary>
/// <param name="notifierService"></param>
/// <param name="blockManager"></param>
public class BlockWritService(INotifierService notifierService, IBlockManager blockManager) :
    BasicBlockService(notifierService, blockManager), IBlockWritService
{
    /// <inheritdoc/>
    public async Task<(BlockResultCode resultCode, BlockStatusCode blockStatusCode)>
        EnqueueOrExecuteAtomicOperationsAsync(string blockId, List<AtomicOperationRequestDto> operations)
    {
        var (atomicOp, blockStatus) =
            await this.blockManager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations.ToAtomicOperations());
        if (blockStatus == null)
            return (BlockResultCode.NotFound, BlockStatusCode.NotFound);

        bool hasBlockStatusError = atomicOp.HasError<BlockStatusError>();
        var resultCode = hasBlockStatusError ? BlockResultCode.Success : BlockResultCode.Error;

        foreach (var error in atomicOp.Errors)
            Log.Error(error.Message);

        foreach (var warning in atomicOp.HandledIssue())
            Log.Warning(warning.Message);

        if (hasBlockStatusError)
            return (resultCode, blockStatus.Value);

        var successes = atomicOp.Value.ToList();

        if (successes.Any())
            await this.notifierService.NotifyStateUpdateAsync(blockId, successes.Select(x => x.EntityId));

        return (resultCode, blockStatus.Value);
    }

    /// <inheritdoc/>
    public async Task<UpdateResult> UpdateBlockGameStateAsync(
        string blockId, Dictionary<string, object?> settingsToUpdate)
        => await this.blockManager.UpdateBlockGameStateAsync(blockId, settingsToUpdate);

    /// <inheritdoc/>
    public async Task ApplyResolvedCommandsAsync(string blockId, List<AtomicOperationRequestDto> resolvedCommands)
    {
        var atomicOp = await this.blockManager.ApplyResolvedCommandsAsync(blockId, resolvedCommands.ToAtomicOperations());

        // 提取最终的状态码
        var blockStatus = await this.GetBlockAsync(blockId);

        if (blockStatus != null)
            await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, blockStatus.StatusCode);

        var successes = atomicOp.Value.ToList();

        if (successes.Any())
            await this.notifierService.NotifyStateUpdateAsync(blockId, successes.Select(x => x.EntityId));
    }


    // --- 和 Workflow 交互的部分 ---

    /// <inheritdoc/>
    public async Task<LoadingBlockStatus?> CreateChildBlockAsync(string parentBlockId,
        Dictionary<string, object?> triggerParams)
    {
        var newBlock = await this.blockManager.CreateChildBlock_Async(parentBlockId, triggerParams);

        if (newBlock == null)
            return null;
        // Notify about the new block and parent update
        await this.notifierService.NotifyBlockStatusUpdateAsync(newBlock.Block.BlockId, newBlock.StatusCode);
        // Maybe notify about parent's selection change? Or batch updates.

        return newBlock;
    }


    /// <inheritdoc/>
    public async Task<Result<BlockStatus>> HandleWorkflowCompletionAsync(string blockId, string requestId, bool success,
        string rawText,
        List<AtomicOperationRequestDto> firstPartyCommands, Dictionary<string, object?> outputVariables)
    {
        var val = await this.blockManager.HandleWorkflowCompletionAsync(blockId, success, rawText, firstPartyCommands.ToAtomicOperations(),
            outputVariables);
        if (val.TryPickT3(out var ErrorOrWarning, out var IdleOrErrorOrConflictBlock))
            return Result.Ok().WithReason(ErrorOrWarning);

        if (IdleOrErrorOrConflictBlock.TryPickT0(out var tempTuple, out var conflictOrErrorBlock))
        {
            var (IdleBlock, results) = tempTuple;
            return IdleBlock;
        }

        if (conflictOrErrorBlock.TryPickT1(out var errorBlock, out var conflictBlock))
        {
            Log.Error("工作流执行失败，block进入错误状态。");
            return errorBlock;
        }

        Log.Info("工作流生成的指令和当前修改存在冲突，等待手动解决。");

        await this.notifierService.NotifyConflictDetectedAsync(new ConflictDetectedDto(BlockId: blockId,
            RequestId: requestId,
            AiCommands: conflictBlock.AiCommands.ToAtomicOperationRequests(),
            UserCommands: conflictBlock.UserCommands.ToAtomicOperationRequests(),
            ConflictingAiCommands: conflictBlock.conflictingAiCommands.ToAtomicOperationRequests(),
            ConflictingUserCommands: conflictBlock.conflictingUserCommands.ToAtomicOperationRequests()));
        return conflictBlock;
    }

    /// <inheritdoc/>
    public async Task<BlockResultCode> UpdateBlockDetailsAsync(string blockId, UpdateBlockDetailsDto updateDto)
    {
        // 参数验证可以在这里做，或者委托给 Manager
        if (updateDto.Content != null || updateDto.MetadataUpdates != null)
            return await this.blockManager.UpdateBlockDetailsAsync(blockId, updateDto.Content, updateDto.MetadataUpdates);

        // 没有提供任何更新内容，可以认为操作“成功”但无效果，或返回 BadRequest
        // 为了简单，我们认为这是一个无操作的成功
        Log.Debug($"Block '{blockId}': 收到空的更新请求，无操作。");
        return BlockResultCode.InvalidInput;
    }
}