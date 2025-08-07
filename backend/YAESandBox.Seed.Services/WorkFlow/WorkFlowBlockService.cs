using YAESandBox.Seed.Block;
using YAESandBox.Seed.Block.BlockManager;
using YAESandBox.Seed.DTOs;
using YAESandBox.Seed.Services.InterFaceAndBasic;
using YAESandBox.Depend;
using YAESandBox.Depend.Results;

namespace YAESandBox.Seed.Services.WorkFlow;

/// <summary>
/// 工作流的Block服务
/// </summary>
/// <param name="notifierService"></param>
/// <param name="blockManager"></param>
public class WorkFlowBlockService(INotifierService notifierService, IBlockManager blockManager)
    : IWorkFlowBlockService
{
    private INotifierService NotifierService { get; } = notifierService;
    private IBlockManager BlockManager { get; } = blockManager;

    /// <inheritdoc/>
    public async Task<LoadingBlockStatus?> CreateChildBlockAsync(string parentBlockId, string workFlowName,
        Dictionary<string, string> workflowInputs)
    {
        var childBlock = await this.BlockManager.CreateChildBlock_Async(parentBlockId, workFlowName, workflowInputs);
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
        var atomicOp = await this.BlockManager.ApplyResolvedCommandsAsync(blockId, resolvedCommands.ToAtomicOperations());

        // 提取最终的状态码
        var blockStatus = await this.BlockManager.GetBlockAsync(blockId);

        if (blockStatus != null)
            await this.NotifierService.NotifyBlockStatusUpdateAsync(blockId, blockStatus.StatusCode);


        var successes = atomicOp.GetSuccessData().ToList();
        if (successes.Any())
            await this.NotifierService.NotifyBlockUpdateAsync(blockId, successes.Select(x => x.EntityId));
    }

    /// <inheritdoc/>
    public async Task<Result<BlockStatus>> HandleWorkflowCompletionAsync(string blockId, string requestId, bool success,
        string rawText,
        List<AtomicOperationRequestDto> firstPartyCommands, Dictionary<string, object?> outputVariables)
    {
        var val = await this.BlockManager.HandleWorkflowCompletionAsync(blockId, success, rawText, firstPartyCommands.ToAtomicOperations(),
            outputVariables);
        if (val.TryPickT3(out var errorOrWarning, out var idleOrErrorOrConflictBlock))
            return errorOrWarning;

        if (idleOrErrorOrConflictBlock.TryPickT0(out var tempTuple, out var conflictOrErrorBlock))
        {
            var (idleBlock, results) = tempTuple;
            return idleBlock;
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
        var loadingStatusResult = await this.BlockManager.StartRegenerationAsync(blockId);

        if (!loadingStatusResult.TryGetValue(out var loadingStatus))
            return loadingStatusResult;
        // *** 成功启动，由 Service 层负责发送通知 ***
        Log.Info($"BlockWritService: Block '{blockId}' 已成功启动重新生成流程，状态转为 Loading。");
        await this.NotifierService.NotifyBlockStatusUpdateAsync(blockId, loadingStatus.StatusCode);
        return loadingStatusResult;
    }
}