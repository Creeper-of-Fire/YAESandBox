using OneOf;
using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Depend;

namespace YAESandBox.Core.Block;

/// <summary>
/// 表示 Block 的不同状态。
/// </summary>
public enum BlockStatusCode
{
    /// <summary>
    /// Block 正在由一等公民工作流处理（例如 AI 生成内容、执行指令）。
    /// 针对此 Block 的修改将被暂存。
    /// </summary>
    Loading,

    /// <summary>
    /// Block 已生成，处于空闲状态，可以接受修改或作为新 Block 的父级。
    /// </summary>
    Idle,

    /// <summary>
    /// 工作流执行完毕，但检测到与暂存的用户指令存在冲突，等待解决。
    /// </summary>
    ResolvingConflict,

    /// <summary>
    /// Block 处理过程中发生错误。
    /// </summary>
    Error
}

public interface IBlockStatus
{
    public Block block { get; init; }
    public BlockStatusCode StatusCode { get; }
}

/// <summary>
/// 表示 Block 的不同状态。
/// </summary>
public class BlockStatus(Block block) : IBlockStatus
{
    public Block block { get; init; } = block;

    public BlockStatusCode StatusCode
    {
        get
        {
            return this switch
            {
                LoadingBlockStatus => BlockStatusCode.Loading,
                IdleBlockStatus => BlockStatusCode.Idle,
                ConflictBlockStatus => BlockStatusCode.ResolvingConflict,
                ErrorBlockStatus => BlockStatusCode.Error,
                _ => throw new InvalidOperationException($"Block '{this.block.BlockId}' 处于错误的状态."),
            };
        }
    }

    // --- 当前世界状态 ---
    /// <summary>
    /// 获取当前可供外部交互（读取和修改）的世界状态。
    /// 根据 Block 状态返回 wsPostUser 或 wsTemp。
    /// </summary>
    /// <exception cref="InvalidOperationException">如果 Block 处于意外状态或内部状态不一致。</exception>
    public WorldState CurrentWorldState
    {
        get
        {
            switch (this)
            {
                case IdleBlockStatus:
                case ErrorBlockStatus:
                    if (this.block.wsPostUser != null)
                        return this.block.wsPostUser;
                    // 理论上不应发生，wsPostUser 在进入 Idle/Error 前应已创建
                    Log.Error(
                        $"Block '{this.block.BlockId}' is Idle/Error but wsPostUser is null. 尝试拷贝WsInput以修复.");
                    this.block.wsPostUser = this.block.wsInput.Clone(); // 尝试恢复
                    return this.block.wsPostUser;

                case LoadingBlockStatus:
                case ConflictBlockStatus:
                    if (this.block.wsTemp != null)
                        return this.block.wsTemp;
                    // 理论上不应发生，wsTemp 在进入 Loading/Resolving 前应已创建
                    Log.Error(
                        $"Block '{this.block.BlockId}' is Loading/Resolving but wsTemp is null. 尝试拷贝WsInput以修复.");
                    this.block.wsTemp = this.block.wsInput.Clone(); // 尝试恢复
                    return this.block.wsTemp;
                default:
                    throw new InvalidOperationException($"Block '{this.block.BlockId}' 处于错误的状态.");
            }
        }
    }
}

/// <summary>
/// Block 已生成，处于空闲状态，可以接受修改或作为新 Block 的父级。
/// </summary>
public class IdleBlockStatus(Block block) : BlockStatus(block)
{
    /// <summary>
    /// 将指定的一系列操作应用到当前唯一允许的 WorldState，允许部分成功部分失败。
    /// 不在内部使用。
    /// </summary>
    /// <param name="operations"></param>
    /// <returns></returns>
    public List<OperationResult> ApplyOperations(List<AtomicOperation> operations)
    {
        var results = Block.ApplyOperationsTo(this.CurrentWorldState, operations);
        return results;
    }

    public (string newBlockId, LoadingBlockStatus newChildblock) CreateNewChildrenBlock()
    {
        string newBlockId = $"blk_{Guid.NewGuid()}";
        if (this.block.wsPostUser == null)
        {
            Log.Error($"block错误，空闲状态必定创建了wsPostUser，请检查。已自动修复。");
            this.block.wsPostUser = this.block.wsInput.Clone();
        }

        var newChildBlock = Block.CreateBlock(newBlockId, this.block.BlockId,
            this.block.wsPostUser, this.block.gameState, this.block.TriggeredChildParams);
        return (newBlockId, newChildBlock);
    }
}

/// <summary>
/// Block 正在由一等公民工作流处理（例如 AI 生成内容、执行指令）。
/// 针对此 Block 的修改将被暂存。
/// </summary>
public class LoadingBlockStatus(Block block) : BlockStatus(block)
{
    /// <summary>
    /// 将指定的一系列操作应用到当前唯一允许的 WorldState 并拷贝到 PendingUserCommands，允许部分成功部分失败。
    /// 不在内部使用。
    /// </summary>
    /// <param name="operations"></param>
    /// <returns></returns>
    public List<OperationResult> ApplyOperations(List<AtomicOperation> operations)
    {
        var results = Block.ApplyOperationsTo(this.CurrentWorldState, operations);
        this.block.PendingUserCommands.AddRange(results.FindAllOK());
        return results;
    }

    /// <summary>
    /// 尝试完成工作流并进入 Idle 状态，如果存在冲突，则进入 Conflict 状态。
    /// </summary>
    /// <param name="rawContent"></param>
    /// <param name="pendingAICommands"></param>
    /// <returns>BlockStatusIdle或BlockStatusConflict</returns>
    internal OneOf<
            (OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results), ConflictBlockStatus>
        TryFinalizeSuccessfulWorkflow(string rawContent, List<AtomicOperation> pendingAICommands)
    {
        (bool hasConflict, var conflictAI, var conflictUser) =
            this.block.DetectAndHandleConflicts(this.block.PendingUserCommands, pendingAICommands);

        if (!hasConflict)
        {
            return this._FinalizeSuccessfulWorkflow(rawContent,
                [..pendingAICommands.Concat(this.block.PendingUserCommands)]);
        }

        if (conflictAI == null || conflictUser == null)
        {
            Log.Error($"Block '{this.block.BlockId}' 有冲突，但是冲突内容为空。");
            return this._FinalizeSuccessfulWorkflow(rawContent,
                [..pendingAICommands.Concat(this.block.PendingUserCommands)]);
        }

        Log.Info($"Block '{this.block.BlockId}' has conflict commands. Entering Conflict State.");
        return this.EnterConflictState(rawContent, conflictAI, conflictUser);
    }

    /// <summary>
    /// 确认工作流成功结束，并进入 Idle 状态。
    /// </summary>
    /// <param name="rawContent"></param>
    /// <param name="commands"></param>
    /// <returns></returns>
    internal (OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results)
        _FinalizeSuccessfulWorkflow(string rawContent,
            List<AtomicOperation> commands)
    {
        OneOf<IdleBlockStatus, ErrorBlockStatus> newSelf = new IdleBlockStatus(this.block);

        this.block.wsPostAI = this.block.wsInput.Clone();
        var applyResults = Block.ApplyOperationsTo(this.block.wsPostAI, commands);
        if (applyResults.IfAtLeastOneFail())
        {
            // 进入 Error 状态
            newSelf = new ErrorBlockStatus(this.block);
            this.block.BlockContent = rawContent;
            this.block.Metadata["Error"] = applyResults.FindAllFail();
            this.block.wsTemp = null;
            //TODO 这一块的逻辑可能还改一改
            this.block.wsPostAI = null; // 失败了，不保留可能不一致的状态
            this.block.wsPostUser = null; // 同样不设置
            Log.Error($"Block '{this.block.BlockId}': Finalization failed. StatusCode set to Error.");
            return (newSelf, applyResults);
        }

        this.block.BlockContent = rawContent;
        this.block.PendingUserCommands.Clear();
        this.block.wsTemp = null;
        this.block.wsPostUser = this.block.wsPostAI.Clone();
        Log.Info($"Block '{this.block.BlockId}': Workflow finalized successfully. StatusCode set to Idle.");
        return (newSelf, applyResults);
    }

    /// <summary>
    /// 进入冲突状态，并记录 AI 和用户提交的命令。
    /// </summary>
    /// <param name="rawContent"></param>
    /// <param name="aiCommands"></param>
    /// <param name="userCommands"></param>
    /// <returns></returns>
    private ConflictBlockStatus EnterConflictState(string rawContent, List<AtomicOperation> aiCommands,
        List<AtomicOperation> userCommands)
    {
        var newSelf = new ConflictBlockStatus(this.block); // 进入冲突状态

        //TODO 解决冲突的逻辑
        this.block.BlockContent = rawContent;
        newSelf.conflictingAiCommands = aiCommands;
        newSelf.conflictingUserCommands = userCommands;
        this.block.wsPostAI = null; // 冲突时尚未生成
        this.block.wsPostUser = null; // 冲突时尚未生成
        Log.Info($"Block '{this.block.BlockId}': Entering conflict state.");
        return newSelf;
    }

    /// <summary>
    /// 当Loading时，强制进入 Idle 状态
    /// </summary>
    /// <returns></returns>
    internal IdleBlockStatus ForceIdleState()
    {
        var newSelf = new IdleBlockStatus(this.block);

        this.block.wsPostAI = this.block.wsInput.Clone(); // wsPostAI 直接变为 wsInput 的副本
        this.block.wsPostUser = this.block.wsPostAI.Clone(); // 强行创建 wsPostUser
        Log.Info($"Block '{this.block.BlockId}': Force idle state.");
        return newSelf;
    }
}

/// <summary>
/// 工作流执行完毕，但检测到与暂存的用户指令存在冲突，等待解决。
/// </summary>
public class ConflictBlockStatus(Block block) : BlockStatus(block)
{
    // --- 冲突信息 (仅在 ResolvingConflict 状态下有意义) ---
    public List<AtomicOperation>? conflictingAiCommands { get; set; }
    public List<AtomicOperation>? conflictingUserCommands { get; set; }

    /// <summary>
    /// 冲突已由用户解决。
    /// 将 Block 从 ResolvingConflict 状态转换为 Idle 状态。
    /// </summary>
    /// <param name="rawContent"></param>
    /// <param name="resolvedCommands">用户提交的最终指令列表。</param>
    /// <returns>如果最终化成功则为 true，否则 false。</returns>
    internal (OneOf<IdleBlockStatus, ErrorBlockStatus> blockStatus, List<OperationResult> results)
        FinalizeConflictResolution(string rawContent,
            List<AtomicOperation> resolvedCommands)
    {
        var newSelf = new LoadingBlockStatus(this.block);

        this.conflictingAiCommands = null;
        this.conflictingUserCommands = null;

        return newSelf._FinalizeSuccessfulWorkflow(rawContent, resolvedCommands);
    }
}

/// <summary>
/// Block 处理过程中发生错误。
/// </summary>
public class ErrorBlockStatus(Block block) : BlockStatus(block);