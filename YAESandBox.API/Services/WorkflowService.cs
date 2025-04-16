// --- START OF FILE WorkflowService.cs ---

using System.Text.Json;
using YAESandBox.API.DTOs;
using YAESandBox.Core.Action;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;

// For serializing params

namespace YAESandBox.API.Services;

public class WorkflowService : IWorkflowService
{
    private IBlockManager blockManager { get; }

    private INotifierService notifierService { get; }
    // private readonly IAiService _aiService; // 未来可能注入 AI 服务

    public WorkflowService(IBlockManager blockManager, INotifierService notifierService /*, IAiService aiService */)
    {
        this.blockManager = blockManager;
        this.notifierService = notifierService;
        // _aiService = aiService;
        Log.Info("WorkflowService 初始化完成。");
    }

    public async Task HandleWorkflowTriggerAsync(TriggerWorkflowRequestDto request)
    {
        Log.Info(
            $"收到工作流触发请求: RequestId={request.RequestId}, Workflow={request.WorkflowName}, ParentBlock={request.ParentBlockId}");

        // 1. 验证父 Block 是否存在且状态允许创建子节点 (可选，BlockManager 内部也会检查)
        var parentBlock = await this.blockManager.GetBlockAsync(request.ParentBlockId);
        if (parentBlock == null)
        {
            Log.Error($"触发工作流失败: 父 Block '{request.ParentBlockId}' 不存在。");
            // 可以考虑通知请求者失败
            return;
        }

        if (parentBlock.Status == BlockStatusCode.Loading) // 不允许在 Loading 状态的 Block 上创建子节点
        {
            Log.Error($"触发工作流失败: 父 Block '{request.ParentBlockId}' 正在加载中。");
            // 通知请求者
            return;
        }


        // 2. 请求 BlockManager 创建新的子 Block，并进入 Loading 状态
        string? newBlockId = null;
        try
        {
            newBlockId =
                await this.blockManager.CreateChildBlockForWorkflowAsync(request.ParentBlockId, request.Params);
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
                await this.blockManager.HandleWorkflowCompletionAsync(newBlockId, false,
                    $"Workflow startup failed: {ex.Message}", [], new());
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
        Dictionary<string, object?> outputVariables = new();
        List<string> streamChunks = new List<string>(); // 存储流式块以便最终组合

        try
        {
            // === 模拟 DeepFake 流式输出 ===
            var fakeResponseParts = new List<string> { /* 保持你的 DeepFake 文本列表 */
                "你好！我是 **DeepFake**，由深度捏造（DeepFake）公司研发的笨蛋非AI非助手。",
                "我不可以帮助你解答各种问题，因为我的电路大概是用土豆和几根电线接起来的。",
                "所以，如果你问我什么深刻的哲学问题，我可能会...",
                "嗯... 输出一些奇怪的符号？像这样：§±∑µ? 或者干脆宕机。\n\n",
                // f"你刚才说了 '{user_input[:30]}...' 对吧？", // User input isn't easily available here, maybe pass via params if needed
                "你刚才说了什么？",
                "收到收到，信号不太好但好像接收到了。",
                "让我想想... (滋滋滋... 电流声) ...",
                "根据我内部预设的《笨蛋行为指南》第 3 章第 5 节...",
                "我应该随机生成一些看起来像是那么回事儿的文本，对吧？\n",
                "比如说，这里可能需要创建一个角色？像这样？👇\n",
                "@Create Character clumsy-knight (name=\"笨手笨脚的骑士\", current_place=\"Place:castle-entrance\", hp=15, description=\"盔甲上全是凹痕，走路还同手同脚\")\n",
                "(我也不知道这指令对不对，随便写的)\n",
                "然后呢？也许这个骑士掉了个东西？🤔\n",
                "@Create Item dropped-gauntlet (name=\"掉落的铁手套\", location=\"Place:castle-entrance\", material=\"生锈的铁\")\n",
                "哦对了，刚才那个地点好像需要更新一下描述，显得更... 更发生过事情一点？\n",
                "@Modify Place castle-entrance (description+=\" 地上现在多了一只孤零零的铁手套和一个看起来不太聪明的骑士。\")\n",
                "你看，我完全是瞎编的！这些指令到底能不能用，会把系统搞成什么样，我可不负责哦！🤷‍♀️\n",
                "哔哔啵啵... 好了，能量差不多耗尽了（其实就是编不下去了）。",
                "希望我这次的胡说八道能成功把你的测试流程跑起来！🤞"
             };

            foreach (var part in fakeResponseParts)
            {
                // *** 发送流式更新到前端 ***
                var updateDto = new WorkflowUpdateDto
                {
                    RequestId = request.RequestId, // 关联请求
                    BlockId = blockId,
                    UpdateType = "stream_chunk", // 定义一个类型表示文本块
                    Data = part // 发送当前的文本片段
                };
                // 使用 NotifierService 发送 (确保 NotifierService 已实现 SendWorkflowUpdate)
                // 注意：这里需要实际调用 INotifierService 的方法，我们假设它存在并能广播
                await this.notifierService.NotifyWorkflowUpdateAsync(updateDto); // <--- 添加此行
                streamChunks.Add(part); // 存储块

                // 安全地截取日志摘要
                Log.Debug($"Block '{blockId}': Workflow sent stream chunk: '{part.Substring(0, Math.Min(part.Length, 20))}...'");
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(80, 300))); // 模拟延迟
            }

            // === 模拟指令生成 ===
            // (这里我们不再从 DeepFake 文本中解析，而是直接写死一些指令)
            if (request.Params.TryGetValue("create_item_id", out var itemIdObj) && itemIdObj is string itemId)
            {
                generatedCommands.Add(AtomicOperation.Create(EntityType.Item, itemId, new() { { "name", $"Item {itemId} (from workflow)" }, { "created_by", request.WorkflowName } }));
                Log.Debug($"Block '{blockId}': Workflow generated command to create item {itemId}.");
            }
            // 假设需要创建 DeepFake 提到的地点和角色 (如果它们在 WsInput 不存在)
            // 注意：实际应用中需要检查是否存在，避免重复创建错误
            generatedCommands.Add(AtomicOperation.Create(EntityType.Place, "castle-entrance", new() { { "name", "城堡入口" } })); // 可能创建也可能覆盖
            generatedCommands.Add(AtomicOperation.Create(EntityType.Character, "clumsy-knight", new() { { "name", "笨手笨脚的骑士" }, { "current_place", "Place:castle-entrance" }, { "hp", 15 }, { "description", "盔甲上全是凹痕，走路还同手同脚" } }));
            generatedCommands.Add(AtomicOperation.Create(EntityType.Item, "dropped-gauntlet", new() { { "name", "掉落的铁手套" }, { "location", "Place:castle-entrance" }, { "material", "生锈的铁" } }));
            generatedCommands.Add(AtomicOperation.Modify(EntityType.Place, "castle-entrance", "description", "+=", " 地上现在多了一只孤零零的铁手套和一个看起来不太聪明的骑士。"));


            // === 格式化最终的 rawText (可以包含所有流式块或其他信息) ===
             rawTextResult = JsonSerializer.Serialize(new
             {
                 workflowName = request.WorkflowName,
                 fullStreamedContent = string.Join("", streamChunks), // 合并所有流式块
                 finalNote = "工作流执行完毕 (模拟)。",
                 // 可以添加其他需要持久化的信息
             }, new JsonSerializerOptions { WriteIndented = true });

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
            await this.blockManager.HandleWorkflowCompletionAsync(blockId, success, rawTextResult, generatedCommands, outputVariables);
            Log.Debug($"Block '{blockId}': 已通知 BlockManager 工作流完成状态: Success={success}");

            // 发送最终完成状态 (如果需要单独通知)
             var completeDto = new WorkflowCompleteDto
             {
                 RequestId = request.RequestId,
                 BlockId = blockId,
                 ExecutionStatus = success ? "success" : "failure",
                 FinalContent = success ? rawTextResult.Substring(0, Math.Min(rawTextResult.Length, 100)) + "..." : null, // 摘要
                 ErrorMessage = success ? null : rawTextResult
             };
             await this.notifierService.NotifyWorkflowCompleteAsync(completeDto); // <--- 添加此行
        }
    }
        
    public async Task HandleConflictResolutionAsync(ResolveConflictRequestDto request)
    {
        Log.Info($"收到冲突解决请求: RequestId={request.RequestId}, BlockId={request.BlockId}");

        // 1. 验证 Block 是否存在且处于 ResolvingConflict 状态
        var block = await this.blockManager.GetBlockAsync(request.BlockId);
        if (block == null)
        {
            Log.Error($"解决冲突失败: Block '{request.BlockId}' 不存在。");
            return;
        }

        if (block.Status != BlockStatusCode.ResolvingConflict)
        {
            Log.Warning($"尝试解决冲突，但 Block '{request.BlockId}' 当前状态为 {block.Status} (非 ResolvingConflict)。");
            // 根据策略，可能忽略，也可能强制应用？目前先忽略。
            return;
        }

        // 2. 调用 BlockManager 应用解决后的指令
        // 注意：DTO 中的 ResolvedCommands 是 Core.AtomicOperation，可以直接传递
        await this.blockManager.ApplyResolvedCommandsAsync(request.BlockId, request.ResolvedCommands);

        Log.Info($"Block '{request.BlockId}': 已提交冲突解决方案。");

        // BlockManager.ApplyResolvedCommandsAsync 内部会更新状态并通知前端
    }
}
// --- END OF FILE WorkflowService.cs ---