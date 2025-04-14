using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Depend;

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
    /// 一等公民工作流执行指令期间使用的临时世界状态。完成后会被丢弃。
    /// </summary>
    public WorldState? WsTemp { get; set; } // Now created by BlockManager when needed

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
    public Dictionary<string, object?> TriggeredChildParams { get; set; } = new(); // Changed name for clarity


    /// <summary>
    /// 创建一个新的根 Block。(Internal use or specific scenarios)
    /// </summary>
    public Block(string blockId, WorldState initialWorldState, GameState initialGameState)
    {
        if (string.IsNullOrWhiteSpace(blockId))
            throw new ArgumentException("Block ID cannot be null or whitespace.", nameof(blockId));

        this.BlockId = blockId;
        this.ParentBlockId = null;
        this.WsInput = initialWorldState ?? throw new ArgumentNullException(nameof(initialWorldState));
        // Root node starts with WsPostUser matching WsInput
        this.WsPostUser = initialWorldState.Clone();
        this.GameState = initialGameState ?? throw new ArgumentNullException(nameof(initialGameState));
        this.Metadata["CreationTime"] = DateTime.UtcNow;
        this.Status = BlockStatus.Idle;
    }

    /// <summary>
    /// 创建一个新的子 Block (由 BlockManager 调用)。
    /// </summary>
    public Block(string blockId, string parentBlockId, WorldState wsInput, GameState gameState,
        Dictionary<string, object?> triggerParams)
    {
        if (string.IsNullOrWhiteSpace(blockId))
            throw new ArgumentException("Block ID cannot be null or whitespace.", nameof(blockId));
        if (string.IsNullOrWhiteSpace(parentBlockId))
            throw new ArgumentException("Parent Block ID cannot be null or whitespace.", nameof(parentBlockId));


        this.BlockId = blockId;
        this.ParentBlockId = parentBlockId;
        this.WsInput = wsInput ?? throw new ArgumentNullException(nameof(wsInput)); // Cloned by caller
        this.GameState = gameState ?? throw new ArgumentNullException(nameof(gameState)); // Cloned by caller
        this.Metadata["CreationTime"] = DateTime.UtcNow;
        this.Metadata["TriggerParams"] = triggerParams; // Store trigger params in metadata
        this.Status = BlockStatus.Loading; // Starts in Loading state
        // WsTemp, WsPostAI, WsPostUser are initially null and managed by BlockManager
        this.WsTemp = this.WsInput.Clone(); // CRITICAL: Create WsTemp immediately for interaction during Loading
        Log.Debug($"子 Block '{blockId}': 已创建 WsTemp (基于 WsInput)。");
    }

    /// <summary>
    /// 获取当前应该用于读取或修改的可交互 WorldState。
    /// </summary>
    public WorldState GetTargetWorldStateForInteraction()
    {
        switch (this.Status)
        {
            case BlockStatus.Idle:
            case BlockStatus.Error:
                // Prefer WsPostUser, fallback to WsInput
                if (this.WsPostUser != null) return this.WsPostUser;
                if (this.WsInput != null)
                {
                    Log.Warning($"Block '{this.BlockId}' 处于 {this.Status} 状态，WsPostUser 为 null，返回 WsInput 作为后备。");
                    // Ensure WsPostUser is created if interaction happens based on WsInput in Idle/Error
                    this.WsPostUser = this.WsInput.Clone();
                    return this.WsPostUser;
                }

                throw new InvalidOperationException(
                    $"Block '{this.BlockId}' in state {this.Status} has no WsPostUser or WsInput.");


            case BlockStatus.Loading:
            case BlockStatus.ResolvingConflict
                : // During conflict, interaction might still use WsTemp or be blocked? Let's use WsTemp for reads.
                if (this.WsTemp == null)
                {
                    // Should have been created in constructor or by manager, but as a fallback:
                    Log.Warning(
                        $"Block '{this.BlockId}' in state {this.Status} has null WsTemp. Attempting to create from WsInput.");
                    if (this.WsInput == null)
                        throw new InvalidOperationException(
                            $"Block '{this.BlockId}' in state {this.Status} has no WsInput to create WsTemp from.");
                    this.WsTemp = this.WsInput.Clone();
                }

                return this.WsTemp;

            default:
                throw new InvalidOperationException($"Block '{this.BlockId}' 处于未知或不支持交互的状态: {this.Status}");
        }
    }
}