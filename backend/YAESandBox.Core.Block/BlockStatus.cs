using FluentResults;
using OneOf;
using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Depend;

namespace YAESandBox.Core.Block;

public interface IBlockStatus
{
    public Block Block { get; }
    public BlockStatusCode StatusCode { get; }
}

/// <summary>
/// 表示 Block 的不同状态。
/// </summary>
public class BlockStatus(Block block) : IBlockStatus
{
    public Block Block { get; } = block;

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
                _ => throw new InvalidOperationException($"Block '{this.Block.BlockId}' 处于错误的状态.")
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
                    if (this.Block.wsPostUser != null)
                        return this.Block.wsPostUser;
                    // 理论上不应发生，wsPostUser 在进入 Idle/Error 前应已创建
                    Log.Error(
                        $"Block '{this.Block.BlockId}' is Idle/Error but wsPostUser is null. 尝试拷贝WsInput以修复.");
                    this.Block.wsPostUser = this.Block.wsInput.Clone(); // 尝试恢复
                    return this.Block.wsPostUser;

                case LoadingBlockStatus:
                case ConflictBlockStatus:
                    if (this.Block.wsTemp != null)
                        return this.Block.wsTemp;
                    // 理论上不应发生，wsTemp 在进入 Loading/Resolving 前应已创建
                    Log.Error(
                        $"Block '{this.Block.BlockId}' is Loading/Resolving but wsTemp is null. 尝试拷贝WsInput以修复.");
                    this.Block.wsTemp = this.Block.wsInput.Clone(); // 尝试恢复
                    return this.Block.wsTemp;
                default:
                    throw new InvalidOperationException($"Block '{this.Block.BlockId}' 处于错误的状态.");
            }
        }
    }
}

/// <summary>
/// Block 已生成，处于空闲状态，可以接受修改或作为新 Block 的父级。
/// </summary>
public class IdleBlockStatus(Block block) : BlockStatus(block), ICanApplyOperations
{
    /// <summary>
    /// 将指定的一系列操作应用到当前唯一允许的 WorldState，允许部分成功部分失败。
    /// 不在内部使用。
    /// </summary>
    /// <param name="operations"></param>
    /// <returns></returns>
    public Result<IEnumerable<AtomicOperation>> ApplyOperations(IEnumerable<AtomicOperation> operations)
    {
        var results = Block.ApplyOperationsTo(this.CurrentWorldState, operations);
        return results.CollectValue();
    }

    internal (string newBlockId, LoadingBlockStatus newChildblock) CreateNewChildrenBlock(string workFlowName)
    {
        string newBlockId = $"blk_{Guid.NewGuid()}";
        if (this.Block.wsPostUser == null)
        {
            Log.Error($"block错误，空闲状态必定创建了wsPostUser，请检查。已自动修复。");
            this.Block.wsPostUser = this.Block.wsInput.Clone();
        }

        var newChildBlock = Block.CreateBlock(newBlockId, this.Block.BlockId, workFlowName,
            this.Block.wsPostUser, this.Block.GameState, this.Block.TriggeredChildParams);
        return (newBlockId, newChildBlock);
    }
}

public interface ICanApplyOperations
{
    Result<IEnumerable<AtomicOperation>> ApplyOperations(IEnumerable<AtomicOperation> operations);
}

/// <summary>
/// Block 正在由一等公民工作流处理（例如 AI 生成内容、执行指令）。
/// 针对此 Block 的修改将被暂存。
/// </summary>
public class LoadingBlockStatus(Block block) : BlockStatus(block), ICanApplyOperations
{
    /// <summary>
    /// 在 Block 处于 Loading 状态期间，暂存的用户原子化修改指令。
    /// </summary>
    public List<AtomicOperation> PendingUserCommands { get; } = [];

    /// <summary>
    /// 将指定的一系列操作应用到当前唯一允许的 WorldState 并拷贝到 PendingUserCommands，允许部分成功部分失败。
    /// 不在内部使用。
    /// </summary>
    /// <param name="operations"></param>
    /// <returns></returns>
    public Result<IEnumerable<AtomicOperation>> ApplyOperations(IEnumerable<AtomicOperation> operations)
    {
        var results = Block.ApplyOperationsTo(this.CurrentWorldState, operations);
        // 目前直接把成功的添加到 PendingUserCommands 之后可能会进行修改
        this.PendingUserCommands.AddRange(results.SelectSuccessValue());
        return results.CollectValue();
    }

    /// <summary>
    /// 尝试完成工作流并进入 Idle 状态，如果存在冲突，则进入 Conflict 状态。
    /// </summary>
    /// <param name="rawContent"></param>
    /// <param name="pendingAICommands"></param>
    /// <returns></returns>
    [HasBlockStateTransition]
    internal OneOf<(IdleBlockStatus blockStatus, Result<IEnumerable<AtomicOperation>> atomicOp), ConflictBlockStatus>
        TryFinalizeSuccessfulWorkflow(string rawContent, List<AtomicOperation> pendingAICommands)
    {
        (bool hasConflict, var simpleResolvedAiCommands, var simpleResolvedUserCommands, var conflictAI,
                var conflictUser) =
            this.Block.DetectAndHandleConflicts(this.PendingUserCommands, pendingAICommands);

        if (conflictAI == null || conflictUser == null)
        {
            Log.Warning($"Block '{this.Block.BlockId}' 有冲突，但是冲突内容为空。");
            var tuple = this._FinalizeSuccessfulWorkflow(rawContent, [..pendingAICommands.Concat(simpleResolvedUserCommands)]);
            return tuple;
        }

        if (!hasConflict)
        {
            var tuple = this._FinalizeSuccessfulWorkflow(rawContent, [..pendingAICommands.Concat(simpleResolvedUserCommands)]);
            return tuple;
        }


        Log.Info($"Block '{this.Block.BlockId}' has conflict commands. Entering Conflict State.");
        return this.EnterConflictState(rawContent,
            simpleResolvedAiCommands, simpleResolvedUserCommands, conflictAI, conflictUser);
    }

    /// <summary>
    /// 确认工作流成功结束，并进入 Idle 状态。
    /// </summary>
    /// <param name="rawContent"></param>
    /// <param name="commands"></param>
    /// <returns></returns>
    [HasBlockStateTransition]
    internal (IdleBlockStatus blockStatus, Result<IEnumerable<AtomicOperation>> atomicOp)
        _FinalizeSuccessfulWorkflow(string rawContent, List<AtomicOperation> commands)
    {
        var newSelf = new IdleBlockStatus(this.Block);

        this.Block.wsPostAI = this.Block.wsInput.Clone();
        var applyResults = Block.ApplyOperationsTo(this.Block.wsPostAI, commands).CollectValue();
        // if (applyResults.IfAtLeastOneFail())
        // {
        //     // 进入 Error 状态
        //     newSelf = new ErrorBlockStatus(this.Block);
        //     this.Block.BlockContent = rawContent;
        //     this.Block.AddOrSetMetaData("Error", applyResults.FindAllFail());
        //     this.Block.wsTemp = null;
        //     //TODO 这一块的逻辑可能还改一改， 目前全部删掉了
        //     this.Block.wsPostAI = null; // 失败了，不保留可能不一致的状态
        //     this.Block.wsPostUser = null; // 同样不设置
        //     Log.Error($"Block '{this.Block.BlockId}': Finalization failed. StatusCode set to Error.");
        //     return (newSelf, applyResults);
        // }

        this.Block.BlockContent = rawContent;
        this.PendingUserCommands.Clear();
        this.Block.wsTemp = null;
        this.Block.wsPostUser = this.Block.wsPostAI.Clone();
        Log.Info($"Block '{this.Block.BlockId}': Workflow finalized successfully. StatusCode set to Idle.");
        return (newSelf, applyResults);
    }

    /// <summary>
    /// 进入冲突状态，并记录 AI 和用户提交的命令。
    /// </summary>
    /// <param name="rawContent"></param>
    /// <param name="conflictAiCommands"></param>
    /// <param name="conflictUserCommands"></param>
    /// <param name="aiCommands"></param>
    /// <param name="userCommands"></param>
    /// <returns></returns>
    [HasBlockStateTransition]
    private ConflictBlockStatus EnterConflictState(string rawContent,
        List<AtomicOperation> conflictAiCommands,
        List<AtomicOperation> conflictUserCommands,
        List<AtomicOperation> aiCommands,
        List<AtomicOperation> userCommands)
    {
        var newSelf = new ConflictBlockStatus(this.Block, conflictAiCommands, conflictUserCommands, aiCommands,
            userCommands); // 进入冲突状态

        //TODO 解决冲突的逻辑
        this.Block.BlockContent = rawContent;
        this.Block.wsPostAI = null; // 冲突时尚未生成
        this.Block.wsPostUser = null; // 冲突时尚未生成
        Log.Info($"Block '{this.Block.BlockId}': Entering conflict state.");
        return newSelf;
    }

    /// <summary>
    /// 当Loading时，强制进入 Idle 状态
    /// </summary>
    /// <returns></returns>
    [HasBlockStateTransition]
    internal IdleBlockStatus ForceIdleState()
    {
        var newSelf = new IdleBlockStatus(this.Block);

        this.Block.wsPostAI = this.Block.wsInput.Clone(); // wsPostAI 直接变为 wsInput 的副本
        this.Block.wsPostUser = this.Block.wsPostAI.Clone(); // 强行创建 wsPostUser
        Log.Info($"Block '{this.Block.BlockId}': Force idle state.");
        return newSelf;
    }

    /// <summary>
    /// 将当前状态转换为 ErrorBlockStatus。
    /// </summary>
    /// <returns></returns>
    [HasBlockStateTransition]
    public ErrorBlockStatus toErrorStatus()
    {
        return new ErrorBlockStatus(this.Block);
    }
}

/// <summary>
/// 工作流执行完毕，但检测到与暂存的用户指令存在冲突，等待解决。
/// </summary>
public class ConflictBlockStatus(
    Block block,
    List<AtomicOperation> conflictAiCommands,
    List<AtomicOperation> conflictUserCommands,
    List<AtomicOperation> aiCommands,
    List<AtomicOperation> userCommands)
    : BlockStatus(block)
{
    // --- 冲突信息 (仅在 ResolvingConflict 状态下有意义) ---
    public List<AtomicOperation> conflictingAiCommands { get; } = conflictAiCommands;
    public List<AtomicOperation> conflictingUserCommands { get; } = conflictUserCommands;

    public List<AtomicOperation> AiCommands { get; } = aiCommands;
    public List<AtomicOperation> UserCommands { get; } = userCommands;

    /// <summary>
    /// 冲突已由用户解决。
    /// 将 Block 从 ResolvingConflict 状态转换为 Idle 状态。
    /// </summary>
    /// <param name="rawContent"></param>
    /// <param name="resolvedCommands">用户提交的最终指令列表。</param>
    /// <returns>如果最终化成功则为 true，否则 false。</returns>
    [HasBlockStateTransition]
    internal (IdleBlockStatus block, Result<IEnumerable<AtomicOperation>> atomicOp)
        FinalizeConflictResolution(string rawContent, List<AtomicOperation> resolvedCommands)
    {
        var newSelf = new LoadingBlockStatus(this.Block);
        return newSelf._FinalizeSuccessfulWorkflow(rawContent, resolvedCommands);
    }
}

/// <summary>
/// Block 处理过程中发生错误。
/// </summary>
public class ErrorBlockStatus(Block block) : BlockStatus(block);

/// <summary>
/// 指示一个方法或代码块包含 Block 状态转换逻辑，
/// 并且调用者（通常是 BlockManager）需要在转换后确保更新 BlockManager.blocks 字典，
/// 并建议使用 EnsureStatusUpdated 进行检查。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
// AttributeTargets.Method: 可以标记方法
// AttributeTargets.Class: 也可以标记整个类（如果类主要负责状态转换）
// Inherited = false: 此属性不被派生类继承
// AllowMultiple = false: 同一个目标上只能应用一次此属性
public sealed class HasBlockStateTransition(string? description = null) : Attribute
{
    /// <summary>
    /// 可选的描述信息，说明转换的性质或需要注意的地方。
    /// </summary>
    public string? Description { get; } = description;
}