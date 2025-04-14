using YAESandBox.Core.Action;
using YAESandBox.Depend;

// For WorldState

namespace YAESandBox.Core.State;

/// <summary>
/// 表示 Block 的不同状态。
/// </summary>
public enum BlockStatus
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

/// <summary>
/// 代表故事树中的一个节点（叙事块）。
/// 封装了特定时间点的状态、内容、关系和元数据。
/// </summary>
public class Block
{
    /// <summary>
    /// Block 的唯一标识符。
    /// </summary>
    public string BlockId { get; init; }

    /// <summary>
    /// 父 Block 的 ID，如果是根节点则为 null。
    /// </summary>
    public string? ParentBlockId { get; init; }

    /// <summary>
    /// 存储子 Block ID 的字典，键是生成顺序/索引。
    /// </summary>
    public Dictionary<int, string> ChildrenInfo { get; } = new();

    /// <summary>
    /// 当前活跃（选中）的子分支的索引。-1 表示没有子节点或未选择。
    /// </summary>
    public int SelectedChildIndex { get; set; } = -1;

    /// <summary>
    /// 输入的世界状态快照（从父节点的 WsPostUser 克隆而来）。创建后只读。
    /// </summary>
    public WorldState WsInput { get; init; }

    /// <summary>
    /// 由一等公民工作流执行指令后生成的世界状态。可能为 null。
    /// </summary>
    public WorldState? WsPostAI { get; set; }

    /// <summary>
    /// 用户在 WsPostAI (或 WsInput) 基础上修改后的世界状态。可能为 null。
    /// 这是生成下一个子节点时 WsInput 的来源。
    /// </summary>
    public WorldState? WsPostUser { get; set; }

    /// <summary>
    /// 一等公民工作流执行指令期间使用的临时世界状态。完成后通常会被丢弃或成为 WsPostAI。
    /// </summary>
    public WorldState? WsTemp { get; set; }

    /// <summary>
    /// 与此 Block 相关的游戏状态设置。
    /// </summary>
    public GameState GameState { get; init; }

    /// <summary>
    /// Block 的主要内容（例如 AI 生成的文本、JSON 配置、HTML 片段）。不过目前来看更有可能是工作流产生的RawText
    /// </summary>
    public string BlockContent { get; set; } = string.Empty;

    /// <summary>
    /// 在 Block 处于 Loading 状态期间，暂存的用户原子化修改指令。
    /// </summary>
    public List<AtomicOperation> PendingUserCommands { get; } = [];

    /// <summary>
    /// 存储与 Block 相关的任意元数据（例如创建时间、触发的工作流名称）。
    /// </summary>
    public Dictionary<string, object?> Metadata { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Block 的当前状态。
    /// </summary>
    public BlockStatus Status { get; set; } // 初始状态可能是 Idle 或 Loading

    /// <summary>
    /// (仅父 Block 存储) 触发子 Block 时使用的参数。
    /// </summary>
    public Dictionary<string, object?> TriggeredChildParams { get; set; } = new();


    /// <summary>
    /// 创建一个新的根 Block。
    /// </summary>
    /// <param name="blockId">Block 的唯一 ID。</param>
    /// <param name="initialWorldState">初始的世界状态。</param>
    /// <param name="initialGameState">初始的游戏状态。</param>
    public Block(string blockId, WorldState initialWorldState, GameState initialGameState)
    {
        if (string.IsNullOrWhiteSpace(blockId))
            throw new ArgumentException("Block ID cannot be null or whitespace.", nameof(blockId));

        this.BlockId = blockId;
        this.ParentBlockId = null; // 根节点没有父节点
        this.WsInput = initialWorldState ?? throw new ArgumentNullException(nameof(initialWorldState)); // 根节点的输入状态
        // 对于根节点，PostUser 通常也等于 Input，除非立即有修改
        this.WsPostUser = initialWorldState.Clone();
        this.GameState = initialGameState ?? throw new ArgumentNullException(nameof(initialGameState));
        this.Metadata["CreationTime"] = DateTime.UtcNow;
        this.Status = BlockStatus.Idle; // 根节点创建后通常是 Idle
    }

    /// <summary>
    /// 创建一个新的子 Block。
    /// </summary>
    /// <param name="blockId">新 Block 的唯一 ID。</param>
    /// <param name="parentBlock">父 Block。</param>
    /// <param name="triggerParams">触发此子 Block 生成所使用的参数。</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException">如果父 Block 的 WsPostUser 为 null。</exception>
    public Block(string blockId, Block parentBlock, Dictionary<string, object?> triggerParams)
    {
        ArgumentNullException.ThrowIfNull(parentBlock);
        if (string.IsNullOrWhiteSpace(blockId))
            throw new ArgumentException("Block ID cannot be null or whitespace.", nameof(blockId));
        if (parentBlock.WsPostUser == null)
            throw new InvalidOperationException($"无法创建子 Block，因为父 Block '{parentBlock.BlockId}' 的 WsPostUser 为 null。");

        this.BlockId = blockId;
        this.ParentBlockId = parentBlock.BlockId;
        this.WsInput = parentBlock.WsPostUser.Clone(); // 从父节点的最终状态克隆
        this.GameState = parentBlock.GameState.Clone(); // 从父节点克隆 GameState
        this.Metadata["CreationTime"] = DateTime.UtcNow;
        this.Status = BlockStatus.Loading; // 新创建的子 Block 通常立即进入 Loading 状态

        // 在父 Block 中记录子 Block 信息和触发参数
        int childIndex = parentBlock.ChildrenInfo.Count; // 基于当前子节点数量分配索引
        parentBlock.ChildrenInfo[childIndex] = this.BlockId;
        parentBlock.TriggeredChildParams = triggerParams ?? new Dictionary<string, object?>();
        parentBlock.SelectedChildIndex = childIndex; // 默认选中新创建的子节点
    }

    /// <summary>
    /// 获取当前应该用于读取或修改的可交互 WorldState。
    /// </summary>
    /// <returns>目标 WorldState。</returns>
    /// <exception cref="InvalidOperationException">如果 Block 状态不允许访问可交互状态。</exception>
    public WorldState GetTargetWorldStateForInteraction()
    {
        // 根据 Block 状态决定返回哪个 WorldState
        switch (this.Status)
        {
            case BlockStatus.Idle:
            case BlockStatus.Error: // 错误状态下也允许查看最后的用户状态
                if (this.WsPostUser != null)
                    return this.WsPostUser;
                Log.Error($"Block '{this.BlockId}' 处于 {this.Status} 状态，但 WsPostUser 为 null，返回 WsInput 作为后备。");
                return this.WsInput; // 或者抛出更严重的异常？

            case BlockStatus.Loading:
            case BlockStatus.ResolvingConflict:
                // 在加载或解决冲突时，外部交互（特别是修改）应该作用于 WsTemp
                // 如果 WsTemp 还未创建（例如刚开始 Loading），则需要创建它
                this.WsTemp ??= (this.WsPostAI ?? this.WsInput).Clone();
                return this.WsTemp;

            default:
                throw new InvalidOperationException($"Block '{this.BlockId}' 处于未知或不支持交互的状态: {this.Status}");
        }
    }
}