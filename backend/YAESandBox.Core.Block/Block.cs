using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FluentResults;
using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;
using static YAESandBox.Core.Action.OperationHandledIssue;

namespace YAESandBox.Core.Block;

public interface IBlockNode
{
    public string BlockId { get; }
    public string? ParentBlockId { get; }
    public List<string> ChildrenList { get; }
}

public abstract class NodeBlock(string blockId, string? parentBlockId) : IBlockNode
{
    /// <summary>
    /// Block 的唯一标识符。
    /// </summary>
    public string BlockId { get; } = blockId;

    /// <summary>
    /// 父 Block 的 ID，如果是根节点则为 null。
    /// </summary>
    public string? ParentBlockId { get; } = parentBlockId;

    /// <summary>
    /// 存储子 Block ID 的列表。
    /// </summary>
    public List<string> ChildrenList { get; } = [];

    // 废弃代码，因为前端维护整个树结构，这些逻辑放在前端执行
    // 对应的持久化逻辑：后端只保存“当前选择路径的最深block的ID”，并且提供一个“列出指定ID全部父类”的接口。并且采取盲存手段，前端自己解析。
    // /// <summary>
    // /// 获取或设置当前活跃（选中）的子 Block 的 ID。
    // /// - Getter:
    // ///   - 如果存在显式设置且有效的 `selectedChildId`，则返回该 ID。
    // ///   - 否则，如果存在子节点，则默认返回最后添加的子节点的 ID。
    // ///   - 如果没有子节点，则返回 null。
    // /// - Setter:
    // ///   - 允许设置一个有效的子 Block ID 进行显式选择，否则自动选择最后一个。
    // /// </summary>
    // public virtual string? SelectedChildId
    // {
    //     get
    //     {
    //         // 检查是否有子节点
    //         if (this.ChildrenList.Count == 0)
    //             return null; // 没有子节点，无法选择
    //
    //         // 为空，或不包含field的情况
    //         if (field == null || !this.ChildrenList.Contains(field))
    //             field = this.ChildrenList.Last(); // 自动设置为最后一项
    //
    //         return field; // 返回显式设置的有效 ID
    //     }
    //     set => field = value ?? this.ChildrenList.Last();
    // }

    public void AddChildren(Block childBlock)
    {
        this.AddChildren(childBlock.BlockId);
    }

    private void AddChildren(string childBlockId)
    {
        this.ChildrenList.Add(childBlockId);
    }
}

/// <summary>
/// 代表故事树中的一个节点（叙事块）。
/// 封装了特定时间点的状态、内容、关系和元数据。
/// </summary>
public class Block : NodeBlock
{
    /// <summary>
    /// 输入的世界状态快照（从父节点的 wsPostUser 克隆而来）。创建后只读。
    /// </summary>
    internal WorldState wsInput { get; }

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
    public GameState GameState { get; }


    /// <summary>
    /// Block 的主要内容（例如 AI 生成的文本、JSON 配置、HTML 片段）。不过目前来看更有可能是工作流产生的RawText
    /// </summary>
    public string BlockContent { get; internal set; } = string.Empty;

    /// <summary>
    /// 触发这个的Bloc内容的工作流的名称。
    /// </summary>
    public string WorkFlowName { get; internal set; }

    /// <summary>
    /// 存储与 Block 相关的任意元数据（例如创建时间、触发的工作流名称）。
    /// 所有的数据必须统一变成字符串，如果要用那么请自行解析。
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata => this._metadata;

    private readonly Dictionary<string, string> _metadata = new();

    internal void AddOrSetMetaData(string key, object value)
    {
        string jsonValue = value as string ?? JsonSerializer.Serialize(value);
        if (this._metadata.ContainsKey(key))
            this._metadata[key] = jsonValue;
        else
            this._metadata.TryAdd(key, jsonValue);
    }

    /// <summary>
    /// (供内部调用) 移除指定的元数据键。
    /// </summary>
    /// <param name="key">要移除的键。</param>
    /// <returns>如果成功移除则为 true，否则为 false（例如键不存在）。</returns>
    internal bool RemoveMetaData(string key)
    {
        return this._metadata.Remove(key);
    }

    /// <summary>
    /// (仅父 Block 存储) 触发子 Block 时使用的参数。只会保存一个，不会为不同的子 Block 保存不同的参数。
    /// </summary>
    public Dictionary<string, object?> TriggeredChildParams { get; internal set; } = new();

    /// <summary>
    /// 创建一个新的子 Block (由 BlockManager 调用)。
    /// </summary>
    private Block(string blockId, string? parentBlockId, string workFlowName, WorldState sourceWorldState, GameState sourceGameState,
        Dictionary<string, object?> triggerParams) : base(blockId, parentBlockId)
    {
        if (string.IsNullOrWhiteSpace(blockId))
            throw new ArgumentException("Block ID cannot be null or whitespace.", nameof(blockId));
        // if (string.IsNullOrWhiteSpace(parentBlockId))
        //     throw new ArgumentException("Parent Block ID cannot be null or whitespace.", nameof(parentBlockId));

        this.AddOrSetMetaData("CreationTime", DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
        this.AddOrSetMetaData("TriggerParams", JsonSerializer.Serialize(triggerParams));
        this.WorkFlowName = workFlowName;

        // --- 在构造函数内部完成克隆 ---
        this.wsInput = sourceWorldState.Clone(); // 创建 wsInput 的隔离副本
        this.GameState = sourceGameState.Clone(); // 创建 GameState 的隔离副本
        this.wsTemp = this.wsInput.Clone(); // 初始 wsTemp 基于内部克隆的 wsInput
    }

    /// <summary>
    /// 创建一个新的 Block。但是返回的是BlockStatus对象
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="parentBlockId"></param>
    /// <param name="workFlowName"></param>
    /// <param name="sourceWorldState"></param>
    /// <param name="sourceGameState"></param>
    /// <param name="triggerParams"></param>
    /// <returns></returns>
    public static LoadingBlockStatus CreateBlock(string blockId, string? parentBlockId, string workFlowName, WorldState sourceWorldState,
        GameState sourceGameState, Dictionary<string, object?>? triggerParams = null)
    {
        triggerParams ??= new Dictionary<string, object?>();
        return new LoadingBlockStatus(
            new Block(blockId, parentBlockId, workFlowName, sourceWorldState, sourceGameState, triggerParams));
    }


    // --- 内部状态转换方法 (由 BlockManager 调用) ---
    // _FinalizeSuccessfulWorkflow, EnterConflictState, FinalizeConflictResolution
    // 这些方法的逻辑基本不变，但它们现在假设调用时 StatusCode 是 Loading 或 ResolvingConflict
    // 并且它们负责最终设置 StatusCode 为 Idle 或 Error，并创建 wsPostAI 和 wsPostUser


    /// <summary>
    /// 检测并处理用户和 AI 命令之间的冲突，采用基于属性的细粒度规则。
    /// </summary>
    /// <param name="pendingUserCommands">用户在 Loading 状态下提交的原始命令。</param>
    /// <param name="pendingAICommands">AI 工作流生成的原始命令。</param>
    /// <returns>一个包含冲突检测结果的元组：
    /// - hasBlockingConflict: bool - 是否存在需要用户解决的阻塞性冲突 (Modify/Modify 同一属性)。
    /// - resolvedAiCommands: List-AtomicOperation - AI 命令列表（当前逻辑下通常与输入相同，除非未来加入 AI 重命名逻辑）。
    /// - resolvedUserCommands: List-AtomicOperation - 经过自动处理（如 Create/Create 重命名）后的用户命令列表。
    /// - conflictingAiForResolution: List-AtomicOperation? - 导致阻塞性冲突的 AI 命令子集 (仅当 hasBlockingConflict 为 true)。
    /// - conflictingUserForResolution: List-AtomicOperation? - 导致阻塞性冲突的用户命令子集 (仅当 hasBlockingConflict 为 true)。
    /// </returns>
    internal (
        bool hasBlockingConflict,
        List<AtomicOperation> resolvedAiCommands,
        List<AtomicOperation> resolvedUserCommands,
        List<AtomicOperation>? conflictingAiForResolution,
        List<AtomicOperation>? conflictingUserForResolution
        ) DetectAndHandleConflicts(
            List<AtomicOperation> pendingUserCommands,
            List<AtomicOperation> pendingAICommands)
    {
        // 初始化返回结果结构
        bool hasBlockingConflict = false;
        List<AtomicOperation> conflictingAiForResolution = [];
        List<AtomicOperation> conflictingUserForResolution = [];

        // 复制原始列表，以便进行修改（特别是用户命令的重命名）
        // AI 命令当前不进行修改，直接引用或浅拷贝即可
        var resolvedAiCommands = new List<AtomicOperation>(pendingAICommands);
        var resolvedUserCommands = new List<AtomicOperation>(pendingUserCommands); // 这将是可能被修改的列表

        // --- 1. 处理 Create/Create 冲突 (自动重命名用户实体) ---
        var aiCreatedEntities = pendingAICommands
            .Where(op => op.OperationType == AtomicOperationType.CreateEntity)
            .Select(op => new TypedID(op.EntityType, op.EntityId))
            .ToHashSet();

        // 存储重命名映射：原始 TypedID -> 新 EntityId
        var renamedUserEntities = new Dictionary<TypedID, string>();

        // 第一次遍历：识别并重命名用户 Create 操作
        for (int i = 0; i < resolvedUserCommands.Count; i++)
        {
            var userOp = resolvedUserCommands[i];
            if (userOp.OperationType != AtomicOperationType.CreateEntity) continue;
            var userTypedId = new TypedID(userOp.EntityType, userOp.EntityId);
            if (!aiCreatedEntities.Contains(userTypedId)) continue;
            // 冲突：AI 也创建了同名同类型实体
            string originalId = userOp.EntityId;
            // 你可以选择不同的重命名策略，例如加后缀或完全随机
            string newId = $"{originalId}_user_created_{Guid.NewGuid().ToString("N")[..6]}";
            renamedUserEntities[userTypedId] = newId;

            // 创建一个新的 AtomicOperation 实例，因为它是 readonly record struct
            resolvedUserCommands[i] = userOp with { EntityId = newId };

            Log.Info(
                $"Block '{this.BlockId}': Create/Create conflict detected for {userTypedId}. User entity automatically renamed to '{newId}'.");
        }

        // 第二次遍历：更新后续引用了被重命名实体的用户操作
        if (renamedUserEntities.Any())
            for (int i = 0; i < resolvedUserCommands.Count; i++)
            {
                var userOp = resolvedUserCommands[i];
                // 检查操作是否针对一个已被重命名的实体 (Create 操作已在上面处理过)
                if (userOp.OperationType == AtomicOperationType.CreateEntity) continue;
                var targetTypedId = new TypedID(userOp.EntityType, userOp.EntityId);
                if (!renamedUserEntities.TryGetValue(targetTypedId, out string? newId)) continue;
                // 更新操作的目标 ID
                resolvedUserCommands[i] = userOp with { EntityId = newId };
                Log.Debug(
                    $"Block '{this.BlockId}': Updated user operation targeting renamed entity {targetTypedId} to use new ID '{newId}'. Operation: {userOp.OperationType}");
            }

        // --- 2. 处理 Modify/Modify 冲突 (同一实体，同一属性) ---
        // 使用 HashSet 存储 AI 修改的 (实体, 属性) 对以提高查找效率
        var aiModifications = pendingAICommands
            .Where(op => op.OperationType == AtomicOperationType.ModifyEntity && op.AttributeKey != null)
            // 使用元组 (TypedID, string) 作为 Key
            .Select(op => (TargetId: new TypedID(op.EntityType, op.EntityId), AttributeKey: op.AttributeKey!))
            .ToHashSet();

        // 查找用户修改操作中与 AI 修改冲突的部分
        foreach (var userOp in resolvedUserCommands) // 遍历可能已重命名的用户命令
        {
            if (userOp.OperationType != AtomicOperationType.ModifyEntity || userOp.AttributeKey == null) continue;
            var userModTarget = (TargetId: new TypedID(userOp.EntityType, userOp.EntityId), userOp.AttributeKey);

            if (!aiModifications.Contains(userModTarget)) continue;
            // 阻塞性冲突发现！
            hasBlockingConflict = true;

            // 添加导致冲突的用户命令
            conflictingUserForResolution.Add(userOp);

            // 找到并添加导致冲突的 AI 命令 (可能多个 AI 命令修改同一属性，虽然少见)
            var conflictingAiOps = pendingAICommands // 从原始 AI 命令中查找
                .Where(aiOp =>
                        aiOp.OperationType == AtomicOperationType.ModifyEntity &&
                        aiOp.AttributeKey == userOp.AttributeKey && // 匹配属性
                        new TypedID(aiOp.EntityType, aiOp.EntityId) == userModTarget.TargetId // 匹配实体
                );
            conflictingAiForResolution.AddRange(conflictingAiOps);

            Log.Warning(
                $"Block '{this.BlockId}': Blocking Modify/Modify conflict detected on Entity '{userModTarget.TargetId}', Attribute '{userModTarget.AttributeKey}'.");
            // 决定是否找到第一个冲突就停止，还是收集所有冲突
            // 当前逻辑会收集所有 Modify/Modify 冲突
        }

        // --- 3. Delete 及其他组合根据规则不视为冲突 ---
        // Delete 操作被忽略
        // Create/Modify 被忽略
        // Modify/Delete 被忽略

        // --- 4. 清理和返回 ---
        if (hasBlockingConflict)
        {
            // 去重，以防万一有重复添加（理论上不应发生，但保险起见）
            conflictingAiForResolution = conflictingAiForResolution.Distinct().ToList();
            conflictingUserForResolution = conflictingUserForResolution.Distinct().ToList();

            // 如果有阻塞性冲突，resolved 命令列表的意义不大，因为不会被直接应用
            // 但为了保持返回结构一致，我们仍然返回它们（可能是修改过的用户列表）
            return (hasBlockingConflict, resolvedAiCommands, resolvedUserCommands, conflictingAiForResolution,
                conflictingUserForResolution);
        }

        // 没有阻塞性冲突
        Log.Info($"Block '{this.BlockId}': No blocking conflicts detected after processing.");
        // 冲突列表为空
        return (false, resolvedAiCommands, resolvedUserCommands, null, null);
    }


    /// <summary>
    /// 辅助方法：将指定的一系列操作应用到 WorldState，允许部分成功部分失败。
    /// </summary>
    /// <returns>返回操作结果</returns>
    internal static List<Result<AtomicOperation>> ApplyOperationsTo(WorldState worldState,
        IEnumerable<AtomicOperation> operations)
    {
        var results = new List<Result<AtomicOperation>>();

        foreach (var op in operations)
            try // 可以选择性地用 try-catch 包裹，以防万一内部方法抛出未预期的异常
            {
                results.Add(ApplyOperationTo(worldState, op)); // 将当前操作的结果添加到列表
            }
            catch (Exception ex)
            {
                // 捕获可能由 worldState 操作（如 SetAttribute, ModifyAttribute）抛出的意外异常
                // 记录这个意外失败，然后继续处理下一个操作
                results.Add(Error(op, $"处理操作时发生意外错误: {ex.Message}").ToResult());
                // 这里可以考虑记录更详细的日志，包括堆栈跟踪 ex.ToString()
            }

        // 所有操作都尝试过后，返回包含所有结果的列表
        return results;
    }

    /// <summary>
    /// 应用单个操作到 WorldState，并返回操作的结果。
    /// </summary>
    /// <param name="worldState">要应用操作的世界状态。</param>
    /// <param name="op">要应用的操作。</param>
    /// <returns>返回操作结果</returns>
    /// <exception cref="UnreachableException">在操作到达不可达代码路径时抛出。</exception>
    private static Result<AtomicOperation> ApplyOperationTo(WorldState worldState, AtomicOperation op)
    {
        switch (op.OperationType)
        {
            case AtomicOperationType.CreateEntity:
                var existing = worldState.FindEntityById(op.EntityId, op.EntityType, false);
                if (existing != null)
                    return Conflict(op, $"实体 '{op.EntityType}:{op.EntityId}' 已存在。").ToResult();

                var newEntity = CreateEntityInstance(op.EntityType, op.EntityId);
                if (op.InitialAttributes != null)
                    foreach (var attr in op.InitialAttributes)
                        newEntity.SetAttribute(attr.Key, attr.Value);

                worldState.AddEntity(newEntity);
                // 成功：记录成功
                return Result.Ok(op);

            case AtomicOperationType.ModifyEntity:
                var entityToModify =
                    worldState.FindEntityById(op.EntityId, op.EntityType, false);
                if (entityToModify == null)
                    return NotFound(op, $"实体 '{op.EntityType}:{op.EntityId}' 未找到或已被销毁。").ToResult();
                // 注意：这里的检查需要根据你的 AtomicOperation 定义调整
                // 如果 AttributeKey 或 ModifyOperator 设计为非空，则这些检查可能不需要或应在创建 op 时完成

                if (op.AttributeKey == null || op.ModifyOperator == null)
                    return InvalidInput(op, $"修改操作 '{op.EntityType}:{op.EntityId}' 的参数 为 null。").ToResult();
                // 再次确认 null 是否是合法的 ModifyValue (根据你的业务逻辑)

                if (op.ModifyValue == null)
                    return InvalidInput(op, $"修改操作 '{op.EntityType}:{op.EntityId}' 的值不能为 null。").ToResult();

                entityToModify.ModifyAttribute(op.AttributeKey, op.ModifyOperator.Value, op.ModifyValue);
                return Result.Ok(op);

            case AtomicOperationType.DeleteEntity:
                var entityToDelete = worldState.FindEntityById(op.EntityId, op.EntityType, false);
                if (entityToDelete != null)
                    entityToDelete.IsDestroyed = true;

                return Result.Ok(op); // 删除操作是幂等的，即使实体不存在或已删除，也视为“逻辑上”成功完成其意图

            default:
                throw new UnreachableException($"未知的操作类型: {op.OperationType}"); // 理论上不应到达这里
        }
    }

    // 辅助方法：创建实体实例
    private static BaseEntity CreateEntityInstance(EntityType type, string id)
    {
        return type switch
        {
            EntityType.Item => new Item(id),
            EntityType.Character => new Character(id),
            EntityType.Place => new Place(id),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    /// <summary>
    /// 构造函数，用于从持久化数据重建 Block。
    /// </summary>
    private Block(
        string blockId,
        string? parentBlockId,
        string workFlowName,
        List<string> childrenIds,
        string blockContent,
        Dictionary<string, string> metadata,
        Dictionary<string, object?> triggeredChildParams,
        GameState gameState, // 传入重建好的 GameState
        WorldState? wsInput, // 传入重建好的 WorldState 快照
        WorldState? wsPostAI,
        WorldState? wsPostUser)
        : base(blockId, parentBlockId)
    {
        // 直接赋值
        this.BlockContent = blockContent;
        this.WorkFlowName = workFlowName;
        this._metadata = metadata; // 注意：这里是引用赋值，如果DTO的字典是新建的就没问题
        this.TriggeredChildParams = triggeredChildParams;
        this.GameState = gameState;
        this.wsInput = wsInput ?? throw new ArgumentNullException(nameof(wsInput),
            $"Block '{blockId}' loaded without wsInput, which is required."); // wsInput 必须有
        this.wsPostAI = wsPostAI;
        this.wsPostUser = wsPostUser;

        // 恢复 ChildrenList
        this.ChildrenList.AddRange(childrenIds);

        // wsTemp 在加载时总是 null，因为非 Idle 状态不保存
        this.wsTemp = null;
    }

    public static IdleBlockStatus CreateBlockFromSave(
        string blockId,
        string? parentBlockId,
        string workFlowName,
        List<string> childrenIds,
        string blockContent,
        Dictionary<string, string> metadata,
        Dictionary<string, object?> triggeredChildParams,
        GameState gameState, // 传入重建好的 GameState
        WorldState? wsInput, // 传入重建好的 WorldState 快照
        WorldState? wsPostAI,
        WorldState? wsPostUser)
    {
        return new IdleBlockStatus(new Block(
            blockId,
            parentBlockId,
            workFlowName,
            childrenIds,
            blockContent,
            metadata,
            triggeredChildParams,
            gameState,
            wsInput,
            wsPostAI,
            wsPostUser));
    }
}