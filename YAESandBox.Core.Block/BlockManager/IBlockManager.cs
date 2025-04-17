using OneOf;
using YAESandBox.Core.Action;
using YAESandBox.Core.State;

namespace YAESandBox.Core.Block;

public interface IBlockManager
{
    IReadOnlyDictionary<string, Block> GetBlocks();
    IReadOnlyDictionary<string, IBlockNode> GetNodeOnlyBlocks();

    /// <summary>
    /// 创建子Block，需要父BlockId和触发参数
    /// </summary>
    /// <param name="parentBlockId"></param>
    /// <param name="triggerParams"></param>
    /// <returns></returns>
    Task<LoadingBlockStatus?> CreateChildBlock_Async(
        string? parentBlockId, Dictionary<string, object?> triggerParams);

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
    Task<UpdateResult> UpdateBlockGameStateAsync(
        string blockId, Dictionary<string, object?> settingsToUpdate);

    /// <summary>
    /// 异步执行或排队原子操作。
    /// 如果为IdleBlockStatus和LoadingBlockStatus，则执行（并在 Loading 期间排队）。
    /// 如果为ConflictBlockStatus或ErrorBlockStatus，则不执行任何东西。
    /// </summary>
    /// <param name="blockId">区块唯一标识符</param>
    /// <param name="operations">待执行的原子操作列表</param>
    /// <returns>返回一个元组，包含区块状态和操作结果列表</returns>
    Task<(OneOf<IdleBlockStatus, LoadingBlockStatus, ConflictBlockStatus, ErrorBlockStatus>? blockStatus,
            List<OperationResult>? results)>
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
    /// 处理已解决冲突的指令。
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="resolvedCommands"></param>
    /// <returns></returns>
    Task<(OneOf<IdleBlockStatus, ErrorBlockStatus>? blockStatus, List<OperationResult>? results)>
        ApplyResolvedCommandsAsync(string blockId, List<AtomicOperation> resolvedCommands);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="success"></param>
    /// <param name="rawText"></param>
    /// <param name="firstPartyCommands">来自第一公民工作流的指令</param>
    /// <param name="outputVariables"></param>
    Task<OneOf<(OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results),
            ConflictBlockStatus, ErrorBlockStatus>?>
        HandleWorkflowCompletionAsync(string blockId, bool success, string rawText,
            List<AtomicOperation> firstPartyCommands, Dictionary<string, object?> outputVariables);
}