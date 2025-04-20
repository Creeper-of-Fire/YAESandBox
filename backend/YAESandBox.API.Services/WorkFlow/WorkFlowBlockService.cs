using FluentResults;
using YAESandBox.API.DTOs;
using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.API.Services.InterFaceAndBasic;
using YAESandBox.Core.Block;
using YAESandBox.Depend;

namespace YAESandBox.API.Services.WorkFlow;

/// <summary>
/// 工作流的Block服务
/// </summary>
/// <param name="notifierService"></param>
/// <param name="blockManager"></param>
public class WorkFlowBlockService(INotifierService notifierService,IBlockManager blockManager) : IWorkFlowBlockService
{
    private INotifierService notifierService { get; } = notifierService;
    private IBlockManager blockManager { get; } = blockManager;
    /// <inheritdoc/>
    public async Task<LoadingBlockStatus?> CreateChildBlockAsync(string parentBlockId, string workFlowName,
        Dictionary<string, object?> triggerParams) =>
        await this.blockManager.CreateChildBlock_Async(parentBlockId, workFlowName, triggerParams);

    /// <inheritdoc/>
    public async Task ApplyResolvedCommandsAsync(string blockId, List<AtomicOperationRequestDto> resolvedCommands)
    {
        var atomicOp = await this.blockManager.ApplyResolvedCommandsAsync(blockId, resolvedCommands.ToAtomicOperations());

        // 提取最终的状态码
        var blockStatus = await this.blockManager.GetBlockAsync(blockId);

        if (blockStatus != null)
            await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, blockStatus.StatusCode);

        var successes = atomicOp.Value.ToList();

        if (successes.Any())
            await this.notifierService.NotifyStateUpdateAsync(blockId, successes.Select(x => x.EntityId));
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
    public async Task<Result<LoadingBlockStatus>> TryStartRegenerationAsync(string blockId, string workFlowName,
        Dictionary<string, object?> triggerParams)
    {
        // 调用 BlockManager 的核心逻辑
        var loadingStatus = await this.blockManager.StartRegenerationAsync(blockId, workFlowName, triggerParams);

        if (!loadingStatus.IsSuccess)
            return loadingStatus;
        // *** 成功启动，由 Service 层负责发送通知 ***
        Log.Info($"BlockWritService: Block '{blockId}' 已成功启动重新生成流程，状态转为 Loading。");
        await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, loadingStatus.Value.StatusCode);

        return loadingStatus;
    }
}