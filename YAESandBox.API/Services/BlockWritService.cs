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
    public async Task<AtomicExecutionResult> EnqueueOrExecuteAtomicOperationsAsync(string blockId,
        List<AtomicOperation> operations)
    {
        var (blockStatus, results) = await this.blockManager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations);
        if (blockStatus == null)
            return AtomicExecutionResult.NotFound;

        var atomicExecutionResult = blockStatus.Value.Match(
            idle => AtomicExecutionResult.Executed,
            loading => AtomicExecutionResult.ExecutedAndQueued,
            conflict => AtomicExecutionResult.ConflictState,
            error => AtomicExecutionResult.Error);

        if (results == null)
            return atomicExecutionResult;
        await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, blockStatus.Value.ForceResult
            <IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus, BlockStatus, BlockStatusCode>
            (target => target.StatusCode));
        if (results.Any())
        {
            await this.notifierService.NotifyStateUpdateAsync(blockId,
                results.Select(x => x.OriginalOperation.EntityId));
        }

        return atomicExecutionResult;
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
        var (blockStatus, results) = await this.blockManager.ApplyResolvedCommandsAsync(blockId, resolvedCommands);
        if (blockStatus == null || results == null)
            return;

        // 提取最终的状态码
        var finalStatusCode = blockStatus.Value.Match(
            idle => idle.StatusCode,
            error => error.StatusCode
        );

        // *** 总是发送最终状态通知 ***
        await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, finalStatusCode);

        if (results.Any())
        {
            await this.notifierService.NotifyStateUpdateAsync(blockId,
                results.Select(x => x.OriginalOperation.EntityId));
        }
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
    public async Task HandleWorkflowCompletionAsync(string blockId, string requestId, bool success, string rawText,
        List<AtomicOperation> firstPartyCommands, Dictionary<string, object?> outputVariables)
    {
        var val = await this.blockManager.HandleWorkflowCompletionAsync(blockId, success, rawText, firstPartyCommands,
            outputVariables);
        if (val == null)
            return;
        if (val.Value.TryPickT0(out var tempTuple, out var conflictOrErrorBlock))
        {
            var (IdleOrErrorBlock, results) = tempTuple;
            // 此时已经处理完成，如果处理的指令存在错误，则进入Error状态
            // TODO 这里处理的太生硬，也许处理的指令有错误直接打印日志然后继续就行了
            return;
        }

        if (conflictOrErrorBlock.TryPickT1(out var errorBlock, out var conflictBlock))
        {
            Log.Error("工作流执行失败，block进入错误状态。");
            return;
        }

        Log.Info("工作流生成的指令和当前修改存在冲突，等待手动解决。");

        await this.notifierService.NotifyConflictDetectedAsync(new ConflictDetectedDto()
        {
            BlockId = blockId,
            RequestId = requestId,
            AiCommands = conflictBlock.AiCommands,
            UserCommands = conflictBlock.UserCommands,
            ConflictingAiCommands = conflictBlock.conflictingAiCommands,
            ConflictingUserCommands = conflictBlock.conflictingUserCommands
        });
    }
}