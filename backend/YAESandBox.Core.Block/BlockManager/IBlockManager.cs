using FluentResults;
using OneOf;
using YAESandBox.Core.Action;
using YAESandBox.Depend;

namespace YAESandBox.Core.Block
{
}

namespace YAESandBox.Core.Block
{
    public interface IBlockManager
    {
        IReadOnlyDictionary<string, Block> GetBlocks();
        IReadOnlyDictionary<string, IBlockNode> GetNodeOnlyBlocks();

        /// <summary>
        /// 创建子Block，需要父BlockId和触发参数
        /// </summary>
        /// <param name="parentBlockId"></param>
        /// <param name="workFlowName"></param>
        /// <param name="triggerParams"></param>
        /// <returns></returns>
        Task<LoadingBlockStatus?> CreateChildBlock_Async(string? parentBlockId, string workFlowName,
            Dictionary<string, string> triggerParams);

        /// <summary>
        /// 获取从根节点到指定块ID可达的最深层叶子节点（根据“最后一个子节点”规则）的完整路径。
        /// 该方法首先确定从起始块向下到最深叶子的路径，然后反向追溯到根节点。
        /// </summary>
        /// <param name="startBlockId">起始块的ID。假定此ID在 'blocks' 字典中有效。</param>
        /// <returns>一个包含从根节点到最深层叶子节点ID的列表。如果路径中遇到数据不一致（如引用了不存在的块），则记录错误并返回空列表。</returns>
        List<string> GetPathToRoot(string startBlockId);

        /// <summary>
        /// 获取BlockID对应的块
        /// </summary>
        /// <param name="blockId"></param>
        /// <returns></returns>
        Task<BlockStatus?> GetBlockAsync(string blockId);

        /// <summary>
        /// 更新block的GameState，它比worldState简单很多，所以简单加锁即可。
        /// 它完全不被一等工作流修改，所以无论BlockStatus是什么，都可以进行修改，不存在任何的冲突。
        /// </summary>
        /// <param name="blockId"></param>
        /// <param name="settingsToUpdate"></param>
        /// <returns></returns>
        Task<BlockResultCode> UpdateBlockGameStateAsync(
            string blockId, Dictionary<string, object?> settingsToUpdate);

        /// <summary>
        /// 异步执行或排队原子操作。
        /// 如果为IdleBlockStatus和LoadingBlockStatus，则执行（并在 Loading 期间排队）。
        /// 如果为ConflictBlockStatus或ErrorBlockStatus，则不执行任何东西。
        /// </summary>
        /// <param name="blockId">区块唯一标识符</param>
        /// <param name="operations">待执行的原子操作列表</param>
        /// <returns>返回一个元组，包含区块状态和操作结果列表（结果包含失败的）</returns>
        Task<(Result<IEnumerable<AtomicOperation>> result, BlockStatusCode? blockStatusCode)>
            EnqueueOrExecuteAtomicOperationsAsync(string blockId, List<AtomicOperation> operations);

        /// <summary>
        /// 将当前 BlockManager 的状态保存到流中。
        /// </summary>
        /// <param name="stream">要写入的流。</param>
        /// <param name="frontEndBlindData">前端提供的盲存数据。</param>
        Task SaveToFileAsync(Stream stream, object? frontEndBlindData);

        /// <summary>
        /// 从流中加载 BlockManager 的状态。
        /// </summary>
        /// <param name="stream">要读取的流。</param>
        /// <returns>恢复的前端盲存数据。</returns>
        Task<object?> LoadFromFileAsync(Stream stream);

        /// <summary>
        /// (内部实现) 手动创建新的 Idle Block。
        /// </summary>
        Task<(ManagementResult result, BlockStatus? newBlockStatus)> InternalCreateBlockManuallyAsync(
            string parentBlockId, Dictionary<string, string>? initialMetadata);

        /// <summary>
        /// (内部实现) 手动删除指定的 Block。
        /// </summary>
        Task<ManagementResult> InternalDeleteBlockManuallyAsync(string blockId, bool recursive, bool force);

        /// <summary>
        /// (内部实现) 手动移动指定的 Block 到新的父节点下。
        /// </summary>
        Task<ManagementResult> InternalMoveBlockManuallyAsync(string blockId, string newParentBlockId);

        /// <summary>
        /// 处理已解决冲突的指令。
        /// </summary>
        /// <param name="blockId"></param>
        /// <param name="resolvedCommands"></param>
        /// <returns></returns>
        Task<Result<IEnumerable<AtomicOperation>>> ApplyResolvedCommandsAsync(string blockId,
            List<AtomicOperation> resolvedCommands);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="blockId"></param>
        /// <param name="success"></param>
        /// <param name="rawText"></param>
        /// <param name="firstPartyCommands">来自第一公民工作流的指令</param>
        /// <param name="outputVariables"></param>
        Task<OneOf<(IdleBlockStatus, Result<IEnumerable<AtomicOperation>>), ConflictBlockStatus, ErrorBlockStatus, IReason>>
            HandleWorkflowCompletionAsync(string blockId, bool success, string rawText, List<AtomicOperation> firstPartyCommands,
                Dictionary<string, object?> outputVariables);


        /// <summary>
        /// 更新指定 Block 的内容和/或元数据。
        /// 仅在 Block 处于 Idle 状态时允许操作。
        /// </summary>
        /// <param name="blockId">要更新的 Block ID。</param>
        /// <param name="newContent">新的 Block 内容。如果为 null 则不更新。</param>
        /// <param name="metadataUpdates">要更新或移除的元数据。Key 为元数据键，Value 为新值（null 表示移除）。如果为 null 则不更新。</param>
        /// <returns>更新操作的结果。</returns>
        Task<BlockResultCode> UpdateBlockDetailsAsync(string blockId, string? newContent, Dictionary<string, string?>? metadataUpdates);


        /// <summary>
        /// 启动对现有 Block 的重新生成过程。
        /// 仅当 Block 处于 Idle 或 Error 状态时有效。
        /// 会将 Block 状态强制转换为 Loading，并重置其派生状态。
        /// </summary>
        /// <param name="blockId">要重新生成的 Block ID。</param>
        /// <returns>
        /// Block当前的状态（必定为LoadingBlockStatus）
        /// </returns>
        Task<Result<LoadingBlockStatus>> StartRegenerationAsync(string blockId);
    }
}