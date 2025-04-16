using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;

namespace YAESandBox.Core.Block;

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
    /// 输入的世界状态快照（从父节点的 wsPostUser 克隆而来）。创建后只读。
    /// </summary>
    internal WorldState wsInput { get; init; }

    /// <summary>
    /// 由一等公民工作流执行指令后生成的世界状态。可能为 null。
    /// </summary>
    internal WorldState? wsPostAI { get; set; }

    /// <summary>
    /// 用户在 wsPostAI 基础上修改后的世界状态。可能为 null。
    /// 这是生成下一个子节点时 wsInput 的来源。
    /// </summary>
    internal WorldState? wsPostUser { get; set; }

    /// <summary>
    /// 一等公民工作流执行指令期间使用的临时世界状态。完成后会被丢弃。
    /// </summary>
    internal WorldState? wsTemp { get; set; }

    /// <summary>
    /// 与此 Block 相关的游戏状态设置。
    /// </summary>
    public GameState gameState { get; init; }

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
    public Dictionary<string, object?> Metadata { get; } = new();

    /// <summary>
    /// (仅父 Block 存储) 触发子 Block 时使用的参数。只会保存一个，不会为不同的子 Block 保存不同的参数。
    /// </summary>
    public Dictionary<string, object?> TriggeredChildParams { get; set; } = new();

    /// <summary>
    /// 创建一个新的子 Block (由 BlockManager 调用)。
    /// </summary>
    private Block(string blockId, string? parentBlockId, WorldState sourceWorldState, GameState sourceGameState,
        Dictionary<string, object?> triggerParams)
    {
        if (string.IsNullOrWhiteSpace(blockId))
            throw new ArgumentException("Block ID cannot be null or whitespace.", nameof(blockId));
        // if (string.IsNullOrWhiteSpace(parentBlockId))
        //     throw new ArgumentException("Parent Block ID cannot be null or whitespace.", nameof(parentBlockId));

        this.BlockId = blockId;
        this.ParentBlockId = parentBlockId;
        this.Metadata["CreationTime"] = DateTime.UtcNow;
        this.Metadata["TriggerParams"] = triggerParams;

        // --- 在构造函数内部完成克隆 ---
        this.wsInput = sourceWorldState.Clone(); // 创建 wsInput 的隔离副本
        this.gameState = sourceGameState.Clone(); // 创建 gameState 的隔离副本
        this.wsTemp = this.wsInput.Clone(); // 初始 wsTemp 基于内部克隆的 wsInput
    }

    /// <summary>
    /// 创建一个新的 Block。但是返回的是BlockStatus对象
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="parentBlockId"></param>
    /// <param name="sourceWorldState"></param>
    /// <param name="sourceGameState"></param>
    /// <param name="triggerParams"></param>
    /// <returns></returns>
    public static LoadingBlockStatus CreateBlock(string blockId, string? parentBlockId, WorldState sourceWorldState,
        GameState sourceGameState, Dictionary<string, object?>? triggerParams = null)
    {
        triggerParams ??= new Dictionary<string, object?>();
        return new LoadingBlockStatus(
            new Block(blockId, parentBlockId, sourceWorldState, sourceGameState, triggerParams));
    }


    // --- 内部状态转换方法 (由 BlockManager 调用) ---
    // _FinalizeSuccessfulWorkflow, EnterConflictState, FinalizeConflictResolution
    // 这些方法的逻辑基本不变，但它们现在假设调用时 StatusCode 是 Loading 或 ResolvingConflict
    // 并且它们负责最终设置 StatusCode 为 Idle 或 Error，并创建 wsPostAI 和 wsPostUser


    /// <summary>
    /// 检测用户提交的命令和AI生成的命令是否有冲突，如果有冲突则进入冲突状态。
    /// </summary>
    /// <param name="pendingUserCommands"></param>
    /// <param name="pendingAICommands"></param>
    /// <returns></returns>
    internal (bool hasConflict, List<AtomicOperation>? conflictingAi, List<AtomicOperation>? conflictingUser)
        DetectAndHandleConflicts(List<AtomicOperation> pendingUserCommands,
            List<AtomicOperation> pendingAICommands)
    {
        // 使用 HashSet 来快速查找冲突
        var userCommandsSet =
            new HashSet<(EntityType, string)>(pendingUserCommands.Select(op => (op.EntityType, op.EntityId)));
        var aiCommandsSet =
            new HashSet<(EntityType, string)>(pendingAICommands.Select(op => (op.EntityType, op.EntityId)));

        // 找出冲突的命令
        var conflictingUser = pendingUserCommands
            .Where(userCommand => aiCommandsSet.Contains((userCommand.EntityType, userCommand.EntityId))).ToList();

        var conflictingAi = pendingAICommands
            .Where(aiCommand => userCommandsSet.Contains((aiCommand.EntityType, aiCommand.EntityId))).ToList();

        if (conflictingUser.Count != 0)
        {
            // 记录冲突
            Log.Warning(
                $"Block '{this.BlockId}': Detected {conflictingUser.Count} conflicts between user and AI commands.");

            // 返回冲突状态
            return (true, conflictingAi, conflictingUser);
        }

        // 没有冲突，继续处理
        Log.Info($"Block '{this.BlockId}': No conflicts detected.");
        return (false, null, null);
    }


    /// <summary>
    /// 辅助方法：将指定的一系列操作应用到 WorldState，允许部分成功部分失败。
    /// </summary>
    /// <returns>一个包含每个操作执行结果的列表。</returns>
    internal static List<OperationResult> ApplyOperationsTo(WorldState worldState, List<AtomicOperation> operations)
    {
        // 用于存储每个操作的结果
        var results = new List<OperationResult>(operations.Count); // 预设容量提高效率

        foreach (var op in operations)
        {
            try // 可以选择性地用 try-catch 包裹，以防万一内部方法抛出未预期的异常
            {
                OperationResult currentResult; // 存储当前操作的结果

                switch (op.OperationType)
                {
                    case AtomicOperationType.CreateEntity:
                        var existing = worldState.FindEntityById(op.EntityId, op.EntityType, includeDestroyed: false);
                        if (existing != null)
                        {
                            // 失败：记录错误，继续下一个操作
                            currentResult = OperationResult.Fail(op, $"实体 '{op.EntityType}:{op.EntityId}' 已存在。");
                        }
                        else
                        {
                            var newEntity = CreateEntityInstance(op.EntityType, op.EntityId);
                            if (op.InitialAttributes != null)
                            {
                                foreach (var attr in op.InitialAttributes)
                                {
                                    newEntity.SetAttribute(attr.Key, attr.Value);
                                }
                            }

                            worldState.AddEntity(newEntity);
                            // 成功：记录成功
                            currentResult = OperationResult.Ok(op);
                        }

                        break; // 结束 case CreateEntity

                    case AtomicOperationType.ModifyEntity:
                        var entityToModify =
                            worldState.FindEntityById(op.EntityId, op.EntityType, includeDestroyed: false);
                        if (entityToModify == null)
                        {
                            currentResult = OperationResult.Fail(op, $"实体 '{op.EntityType}:{op.EntityId}' 未找到或已被销毁。");
                        }
                        // 注意：这里的检查需要根据你的 AtomicOperation 定义调整
                        // 如果 AttributeKey 或 ModifyOperator 设计为非空，则这些检查可能不需要或应在创建 op 时完成
                        else if (op.AttributeKey == null || op.ModifyOperator == null)
                        {
                            currentResult = OperationResult.Fail(op,
                                $"修改操作 '{op.EntityType}:{op.EntityId}' 的参数无效 (AttributeKey 或 Operator 为 null)。");
                        }
                        // 再次确认 null 是否是合法的 ModifyValue (根据你的业务逻辑)
                        else if (op.ModifyValue == null)
                        {
                            currentResult =
                                OperationResult.Fail(op, $"修改操作 '{op.EntityType}:{op.EntityId}' 的值不能为 null。");
                        }
                        else
                        {
                            entityToModify.ModifyAttribute(op.AttributeKey, op.ModifyOperator.Value, op.ModifyValue);
                            currentResult = OperationResult.Ok(op);
                        }

                        break; // 结束 case ModifyEntity

                    case AtomicOperationType.DeleteEntity:
                        var entityToDelete =
                            worldState.FindEntityById(op.EntityId, op.EntityType, includeDestroyed: false);
                        if (entityToDelete != null)
                        {
                            entityToDelete.IsDestroyed = true;
                        }

                        // 删除操作是幂等的，即使实体不存在或已删除，也视为“逻辑上”成功完成其意图
                        currentResult = OperationResult.Ok(op);
                        break; // 结束 case DeleteEntity

                    default:
                        // 理论上不应到达这里，但为了健壮性可以记录一个错误
                        currentResult = OperationResult.Fail(op, $"未知的操作类型: {op.OperationType}");
                        // 或者，如果你确定枚举覆盖完全，可以继续使用 UnreachableException，但它会终止整个过程
                        // throw new UnreachableException($"未知的操作类型: {op.OperationType}");
                        break;
                }

                results.Add(currentResult); // 将当前操作的结果添加到列表
            }
            catch (Exception ex)
            {
                // 捕获可能由 worldState 操作（如 SetAttribute, ModifyAttribute）抛出的意外异常
                // 记录这个意外失败，然后继续处理下一个操作
                results.Add(OperationResult.Fail(op, $"处理操作时发生意外错误: {ex.Message}"));
                // 这里可以考虑记录更详细的日志，包括堆栈跟踪 ex.ToString()
            }
        }

        // 所有操作都尝试过后，返回包含所有结果的列表
        return results;
    }

    // 辅助方法：创建实体实例
    private static BaseEntity CreateEntityInstance(EntityType type, string id)
    {
        return type switch
        {
            EntityType.Item => new Item(id),
            EntityType.Character => new Character(id),
            EntityType.Place => new Place(id),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }
}