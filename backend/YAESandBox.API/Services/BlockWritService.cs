using FluentResults;
using YAESandBox.API.Controllers;
using YAESandBox.API.DTOs;
using YAESandBox.Core.Action;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;
using OneOf;

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
    /// <summary>
    /// 输入原子化指令修改其内容
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="operations"></param>
    /// <returns></returns>
    public async Task<(ResultCode resultCode, BlockStatusCode blockStatusCode)>
        EnqueueOrExecuteAtomicOperationsAsync(string blockId, List<AtomicOperation> operations)
    {
        var (atomicOp, blockStatus) = await this.blockManager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);
        if (blockStatus == null)
            return (ResultCode.NotFound, BlockStatusCode.Error);

        bool hasBlockStatusError = atomicOp.HasError<BlockStatusError>();
        var resultCode = hasBlockStatusError ? ResultCode.Success : ResultCode.Error;

        foreach (var error in atomicOp.Errors)
            Log.Error(error.Message);

        foreach (var warning in atomicOp.Warning())
            Log.Warning(warning.Message);

        if (hasBlockStatusError)
            return (resultCode, blockStatus.Value);

        var successes = atomicOp.Value.ToList();

        if (successes.Any())
            await this.notifierService.NotifyStateUpdateAsync(blockId, successes.Select(x => x.EntityId));

        return (resultCode, blockStatus.Value);
    }

    /// <summary>
    /// 更新 Block 的GameState
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="settingsToUpdate"></param>
    /// <returns></returns>
    public async Task<UpdateResult> UpdateBlockGameStateAsync(
        string blockId, Dictionary<string, object?> settingsToUpdate)
        => await this.blockManager.UpdateBlockGameStateAsync(blockId, settingsToUpdate);

    /// <summary>
    /// 应用用户解决冲突后提交的指令列表。
    /// </summary>
    /// <param name="blockId">需要应用指令的 Block ID。</param>
    /// <param name="resolvedCommands">解决冲突后的指令列表。</param>
    public async Task ApplyResolvedCommandsAsync(string blockId, List<AtomicOperation> resolvedCommands)
    {
        var atomicOp = await this.blockManager.ApplyResolvedCommandsAsync(blockId, resolvedCommands);

        // 提取最终的状态码
        var blockStatus = await this.GetBlockAsync(blockId);
        
        if (blockStatus != null)
            await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, blockStatus.StatusCode);

        var successes = atomicOp.Value.ToList();

        if (successes.Any())
            await this.notifierService.NotifyStateUpdateAsync(blockId, successes.Select(x => x.EntityId));
    }


    // --- 和 Workflow 交互的部分 ---

    /// <summary>
    /// 为新的工作流创建一个子 Block。
    /// </summary>
    /// <param name="parentBlockId">父 Block ID。</param>
    /// <param name="triggerParams">触发工作流的参数。</param>
    /// <returns>新创建的子 Block，如果失败则返回 null。</returns>
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


    /// <summary>
    /// 处理工作流执行完成后的回调。
    /// </summary>
    /// <param name="blockId">完成的工作流对应的 Block ID。</param>
    /// <param name="requestId">请求ID</param>
    /// <param name="success">工作流是否成功执行。</param>
    /// <param name="rawText">工作流生成的原始文本内容。</param>
    /// <param name="firstPartyCommands">工作流生成的原子指令。</param>
    /// <param name="outputVariables">工作流输出的变量 (可选，用于元数据等)。</param>
    /// <returns>返回状态，如果没有找到block或者block在应用前不为loading，则返回空（代表出错）</returns>
    public async Task<Result<BlockStatus>> HandleWorkflowCompletionAsync(string blockId, string requestId, bool success,
        string rawText,
        List<AtomicOperation> firstPartyCommands, Dictionary<string, object?> outputVariables)
    {
        var val = await this.blockManager.HandleWorkflowCompletionAsync(blockId, success, rawText, firstPartyCommands,
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
}