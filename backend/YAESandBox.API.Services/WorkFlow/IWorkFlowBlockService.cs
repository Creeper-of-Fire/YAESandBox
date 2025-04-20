using FluentResults;
using YAESandBox.API.DTOs;
using YAESandBox.Core.Block;

namespace YAESandBox.API.Services.WorkFlow;

public interface IWorkFlowBlockService
{
    /// <summary>
    /// 应用用户解决冲突后提交的指令列表。
    /// </summary>
    /// <param name="blockId">需要应用指令的 Block ID。</param>
    /// <param name="resolvedCommands">解决冲突后的指令列表。</param>
    Task ApplyResolvedCommandsAsync(string blockId, List<AtomicOperationRequestDto> resolvedCommands);

    /// <summary>
    /// 为新的工作流创建一个子 Block。
    /// </summary>
    /// <param name="parentBlockId">父 Block ID。</param>
    /// <param name="workFlowName">触发的工作流名称</param>
    /// <param name="triggerParams">触发工作流的参数。</param>
    /// <returns>新创建的子 Block，如果失败则返回 null。</returns>
    Task<LoadingBlockStatus?> CreateChildBlockAsync(string parentBlockId, string workFlowName,
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
        List<AtomicOperationRequestDto> firstPartyCommands, Dictionary<string, object?> outputVariables);


    /// <summary>
    /// 尝试为指定的 Block 启动重新生成流程，将其状态置为 Loading。
    /// 仅当 Block 处于 Idle 或 Error 状态时有效。
    /// </summary>
    /// <param name="blockId">要重新生成的 Block ID。</param>
    /// <param name="workFlowName">触发工作流的名字</param>
    /// <param name="triggerParams">触发工作流的参数。</param>
    /// <returns>如果成功启动，返回新的 LoadingBlockStatus；否则返回 null。</returns>
    Task<Result<LoadingBlockStatus>> TryStartRegenerationAsync(string blockId, string workFlowName,
        Dictionary<string, object?> triggerParams);
}