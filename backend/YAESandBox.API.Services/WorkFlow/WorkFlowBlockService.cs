using FluentResults;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services.InterFaceAndBasic;
using YAESandBox.Core.Block;
using YAESandBox.Depend;

namespace YAESandBox.API.Services.WorkFlow;

/// <summary>
/// 工作流的Block服务
/// </summary>
/// <param name="notifierService"></param>
/// <param name="blockManager"></param>
public class WorkFlowBlockService(INotifierService notifierService, IBlockManager blockManager)
    : IWorkFlowBlockService
{
    private INotifierService NotifierService { get; } = notifierService;
    private IBlockManager blockManager { get; } = blockManager;

    /// <inheritdoc/>
    public async Task<LoadingBlockStatus?> CreateChildBlockAsync(string parentBlockId, string workFlowName,
        Dictionary<string, string> triggerParams)
    {
        var childBlock = await this.blockManager.CreateChildBlock_Async(parentBlockId, workFlowName, triggerParams);
        if (childBlock != null)
        {
            await this.NotifierService.NotifyBlockUpdateAsync(parentBlockId, BlockDataFields.ChildrenInfo);
            await this.NotifierService.NotifyBlockStatusUpdateAsync(childBlock.Block.BlockId, childBlock.StatusCode);
        }

        return childBlock;
    }

    /// <inheritdoc/>
    public async Task ApplyResolvedCommandsAsync(string blockId, List<AtomicOperationRequestDto> resolvedCommands)
    {
        var atomicOp = await this.blockManager.ApplyResolvedCommandsAsync(blockId, resolvedCommands.ToAtomicOperations());

        // 提取最终的状态码
        var blockStatus = await this.blockManager.GetBlockAsync(blockId);

        if (blockStatus != null)
            await this.NotifierService.NotifyBlockStatusUpdateAsync(blockId, blockStatus.StatusCode);

        var successes = atomicOp.Value.ToList();

        if (successes.Any())
            await this.NotifierService.NotifyBlockUpdateAsync(blockId, successes.Select(x => x.EntityId));
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
        
        await this.NotifierService.NotifyConflictDetectedAsync(blockId);

        return conflictBlock;
    }


    /// <inheritdoc/>
    public async Task<Result<LoadingBlockStatus>> TryStartRegenerationAsync(string blockId)
    {
        // 调用 BlockManager 的核心逻辑
        var loadingStatus = await this.blockManager.StartRegenerationAsync(blockId);

        if (!loadingStatus.IsSuccess)
            return loadingStatus;
        // *** 成功启动，由 Service 层负责发送通知 ***
        Log.Info($"BlockWritService: Block '{blockId}' 已成功启动重新生成流程，状态转为 Loading。");
        await this.NotifierService.NotifyBlockStatusUpdateAsync(blockId, loadingStatus.Value.StatusCode);

        return loadingStatus;
    }
}