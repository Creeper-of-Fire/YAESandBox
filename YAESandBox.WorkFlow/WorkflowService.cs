// --- START OF FILE WorkflowService.cs ---

using System.Text.Json;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;
// For serializing params

namespace YAESandBox.WorkFlow;

public class WorkflowService : IWorkflowService
{
    private readonly IBlockManager _blockManager;
    private readonly INotifierService _notifierService;
    // private readonly IAiService _aiService; // 未来可能注入 AI 服务

    public WorkflowService(IBlockManager blockManager, INotifierService notifierService /*, IAiService aiService */)
    {
        this._blockManager = blockManager;
        this._notifierService = notifierService;
        // _aiService = aiService;
        Log.Info("WorkflowService 初始化完成。");
    }

    public async Task HandleWorkflowTriggerAsync(TriggerWorkflowRequestDto request)
    {
        Log.Info($"收到工作流触发请求: RequestId={request.RequestId}, Workflow={request.WorkflowName}, ParentBlock={request.ParentBlockId}");

        // 1. 验证父 Block 是否存在且状态允许创建子节点 (可选，BlockManager 内部也会检查)
        var parentBlock = await this._blockManager.GetBlockAsync(request.ParentBlockId);
        if (parentBlock == null)
        {
            Log.Error($"触发工作流失败: 父 Block '{request.ParentBlockId}' 不存在。");
            // 可以考虑通知请求者失败
            return;
        }
        if (parentBlock.Status == BlockStatus.Loading) // 不允许在 Loading 状态的 Block 上创建子节点
        {
             Log.Error($"触发工作流失败: 父 Block '{request.ParentBlockId}' 正在加载中。");
             // 通知请求者
             return;
        }


        // 2. 请求 BlockManager 创建新的子 Block，并进入 Loading 状态
        string? newBlockId = null;
        try
        {
            newBlockId = await this._blockManager.CreateChildBlockForWorkflowAsync(request.ParentBlockId, request.Params);
            if (newBlockId == null)
            {
                 Log.Error($"创建子 Block 失败，父 Block: {request.ParentBlockId}");
                 return; // 创建失败，已记录日志
            }
            Log.Info($"为工作流 '{request.WorkflowName}' 创建了新的子 Block: {newBlockId}");

            // 3. 异步执行工作流逻辑 (使用 Task.Run 避免阻塞 Hub 调用线程)
            // 注意：在 Task.Run 中访问 Scoped 服务 (如数据库上下文) 可能需要手动创建 Scope
             _ = Task.Run(() => this.ExecuteWorkflowAsync(request, newBlockId));

        }
        catch (Exception ex)
        {
            Log.Error(ex, $"处理工作流触发时发生异常 (Block 创建阶段): {ex.Message}");
            // 如果 Block 已创建但启动失败，需要将其状态设为 Error
            if (newBlockId != null)
            {
                await this._blockManager.HandleWorkflowCompletionAsync(newBlockId, false, $"Workflow startup failed: {ex.Message}", [], new());
            }
        }
    }

    // 实际执行工作流的私有方法
    private async Task ExecuteWorkflowAsync(TriggerWorkflowRequestDto request, string blockId)
    {
        Log.Debug($"Block '{blockId}': 开始执行工作流 '{request.WorkflowName}'...");
        bool success = false;
        string rawTextResult = string.Empty;
        List<AtomicOperation> generatedCommands = [];
        Dictionary<string, object?> outputVariables = new(); // 工作流输出的变量

        try
        {
            // --- 这里是写死的工作流逻辑 ---
            // TODO: 未来根据 request.WorkflowName 加载并执行动态工作流/脚本

            // 模拟步骤 1: 准备阶段 (无 AI)
            await Task.Delay(100); // 模拟耗时
            string greeting = $"你好！这是为 Block '{blockId}' 执行的 '{request.WorkflowName}' 工作流。";
            outputVariables["initial_message"] = greeting;
             Log.Debug($"Block '{blockId}': Workflow Step 1 complete.");
             // 可以选择性地通过 Notifier 更新前端进度/日志
             // await _notifierService.NotifyWorkflowUpdateAsync(new WorkflowUpdateDto { ... });


            // 模拟步骤 2: 根据参数执行某些操作 (无 AI)
            if (request.Params.TryGetValue("add_description", out var descObj) && descObj is string description)
            {
                 // 假设我们要修改父 Block (或某个实体) 的描述 - 这只是示例
                 // 注意：工作流通常只应该影响新 Block 的 WsInput -> WsPostAI
                 // 这里我们生成一个修改新 Block 中某个实体的指令 (假设实体 'target_entity' 已存在于 WsInput)
                 if (request.Params.TryGetValue("target_entity_id", out var targetIdObj) && targetIdObj is string targetId)
                 {
                    generatedCommands.Add(AtomicOperation.Modify(EntityType.Item, targetId, "description", Operator.Equal, description));
                     Log.Debug($"Block '{blockId}': Workflow generated command to set description for {targetId}.");
                 }
            }
            if (request.Params.TryGetValue("create_item_id", out var itemIdObj) && itemIdObj is string itemId)
            {
                 generatedCommands.Add(AtomicOperation.Create(EntityType.Item, itemId, new() { { "name", $"Item {itemId}" }, { "created_by", request.WorkflowName } }));
                 Log.Debug($"Block '{blockId}': Workflow generated command to create item {itemId}.");
                 outputVariables["created_item"] = itemId;
            }
            await Task.Delay(200); // 模拟耗时
             Log.Debug($"Block '{blockId}': Workflow Step 2 complete.");

            // 模拟步骤 3: "AI" 处理 (实际是延迟 + 固定输出)
             Log.Debug($"Block '{blockId}': Workflow Step 3 - Simulating AI call...");
            await Task.Delay(1500); // 模拟 AI 思考时间
            string aiOutput = $"AI '完成'了对 '{request.WorkflowName}' 的处理。它觉得应该添加一个角色。";
            generatedCommands.Add(AtomicOperation.Create(EntityType.Character, $"char_{Guid.NewGuid().ToString("N")[..4]}", new() { { "name", "AI生成的角色" } }));
             Log.Debug($"Block '{blockId}': Workflow Step 3 - AI simulation complete.");
            outputVariables["ai_result"] = aiOutput;


            // 模拟步骤 4: 总结/格式化输出 (无 AI) - 生成 raw_text
            rawTextResult = JsonSerializer.Serialize(new
            {
                message = outputVariables.GetValueOrDefault("initial_message"),
                createdItem = outputVariables.GetValueOrDefault("created_item"),
                aiSummary = outputVariables.GetValueOrDefault("ai_result"),
                finalNote = "工作流执行完毕。",
                // 可以包含其他需要持久化的变量
            }, new JsonSerializerOptions { WriteIndented = true }); // 使用 JSON 作为 raw_text 格式

            success = true;
             Log.Info($"Block '{blockId}': 工作流 '{request.WorkflowName}' 执行成功。");

        }
        catch (Exception ex)
        {
            success = false;
            rawTextResult = $"工作流 '{request.WorkflowName}' 执行失败: {ex.Message}";
            Log.Error(ex, $"Block '{blockId}': 工作流 '{request.WorkflowName}' 执行过程中发生异常: {ex.Message}");
        }
        finally
        {
            // 4. 通知 BlockManager 工作流执行完毕 (无论成功或失败)
            await this._blockManager.HandleWorkflowCompletionAsync(blockId, success, rawTextResult, generatedCommands, outputVariables);
             Log.Debug($"Block '{blockId}': 已通知 BlockManager 工作流完成状态: Success={success}");

            // 5. (可选) 通过 NotifierService 发送最终完成/错误通知给前端
            // if (success)
            //    await _notifierService.NotifyWorkflowCompleteAsync(new WorkflowCompleteDto { ... });
            // else
            //    await _notifierService.NotifyWorkflowErrorAsync(new WorkflowErrorDto { ... });
        }
    }

    public async Task HandleConflictResolutionAsync(ResolveConflictRequestDto request)
    {
        Log.Info($"收到冲突解决请求: RequestId={request.RequestId}, BlockId={request.BlockId}");

        // 1. 验证 Block 是否存在且处于 ResolvingConflict 状态
        var block = await this._blockManager.GetBlockAsync(request.BlockId);
        if (block == null)
        {
            Log.Error($"解决冲突失败: Block '{request.BlockId}' 不存在。");
            return;
        }
        if (block.Status != BlockStatus.ResolvingConflict)
        {
            Log.Warning($"尝试解决冲突，但 Block '{request.BlockId}' 当前状态为 {block.Status} (非 ResolvingConflict)。");
            // 根据策略，可能忽略，也可能强制应用？目前先忽略。
            return;
        }

        // 2. 调用 BlockManager 应用解决后的指令
        // 注意：DTO 中的 ResolvedCommands 是 Core.AtomicOperation，可以直接传递
        await this._blockManager.ApplyResolvedCommandsAsync(request.BlockId, request.ResolvedCommands);

        Log.Info($"Block '{request.BlockId}': 已提交冲突解决方案。");

        // BlockManager.ApplyResolvedCommandsAsync 内部会更新状态并通知前端
    }
}
// --- END OF FILE WorkflowService.cs ---