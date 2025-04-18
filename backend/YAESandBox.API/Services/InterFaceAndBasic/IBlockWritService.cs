using FluentResults;
using YAESandBox.API.Controllers;
using YAESandBox.Core.Action;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Depend;

namespace YAESandBox.API.Services;

public interface IBlockWritService
{
    /// <summary>
    /// 更新 Block 的GameState
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="settingsToUpdate"></param>
    /// <returns></returns>
    Task<UpdateResult> UpdateBlockGameStateAsync(string blockId, Dictionary<string, object?> settingsToUpdate);

    /// <summary>
    /// 输入原子化指令修改其内容
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="operations"></param>
    /// <returns></returns>
    Task<(ResultCode resultCode, BlockStatusCode blockStatusCode)> EnqueueOrExecuteAtomicOperationsAsync(string blockId, List<AtomicOperation> operations);

    /// <summary>
    /// 应用用户解决冲突后提交的指令列表。
    /// </summary>
    /// <param name="blockId">需要应用指令的 Block ID。</param>
    /// <param name="resolvedCommands">解决冲突后的指令列表。</param>
    Task ApplyResolvedCommandsAsync(string blockId, List<AtomicOperation> resolvedCommands);

    // --- Workflow Interaction Methods ---
    /// <summary>
    /// 为新的工作流创建一个子 Block。
    /// </summary>
    /// <param name="parentBlockId">父 Block ID。</param>
    /// <param name="triggerParams">触发工作流的参数。</param>
    /// <returns>新创建的子 Block，如果失败则返回 null。</returns>
    Task<LoadingBlockStatus?> CreateChildBlockAsync(string parentBlockId,
        Dictionary<string, object?> triggerParams);

    /// <summary>
    /// 处理工作流执行完成后的回调。
    /// </summary>
    /// <param name="blockId">完成的工作流对应的 Block ID。</param>
    /// <param name="requestId">请求ID</param>
    /// <param name="success">工作流是否成功执行。</param>
    /// <param name="rawText">工作流生成的原始文本内容。</param>
    /// <param name="firstPartyCommands">工作流生成的原子指令。</param>
    /// <param name="outputVariables">工作流输出的变量 (可选，用于元数据等)。</param>
    Task<Result<BlockStatus>> HandleWorkflowCompletionAsync(string blockId, string requestId, bool success, string rawText,
        List<AtomicOperation> firstPartyCommands, Dictionary<string, object?> outputVariables);
}