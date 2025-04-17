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

public class WorkflowService(
    IBlockReadService blockReadServices,
    IBlockWritService blockWritServices,
    INotifierService notifierService) : IWorkflowService
{
    private IBlockReadService blockReadServices { get; } = blockReadServices;
    private IBlockWritService blockWritServices { get; } = blockWritServices;
    private INotifierService notifierService { get; } = notifierService;
    // private readonly IAiService _aiService; // 未来可能注入 AI 服务

    /// <summary>
    /// 触发主工作流
    /// </summary>
    /// <param name="request"></param>
    public async Task HandleMainWorkflowTriggerAsync(TriggerWorkflowRequestDto request)
    {
        Log.Info(
            $"GameHub: 收到主工作流触发请求: RequestId={request.RequestId}, Workflow={request.WorkflowName}, ParentBlock={request.ParentBlockId}");
        var childBlock =
            await this.blockWritServices.CreateChildBlockAsync(request.ParentBlockId, request.Params);
        if (childBlock == null)
        {
            Log.Error($"创建子 Block 失败，父 Block: {request.ParentBlockId}");
            return; // 创建失败，已记录日志
        }

        Log.Info($"为工作流 '{request.WorkflowName}' 创建了新的子 Block: {childBlock.Block.BlockId}");
        // 3. 异步执行工作流逻辑 (使用 Task.Run 避免阻塞 Hub 调用线程)
        // 注意：在 Task.Run 中访问 Scoped 服务 (如数据库上下文) 可能需要手动创建 Scope
        _ = Task.Run(() => this.StartMainExecuteWorkflowAsync(request, childBlock.Block.BlockId));
    }

    /// <summary>
    /// 触发微工作流
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task HandleMicroWorkflowTriggerAsync(TriggerMicroWorkflowRequestDto request)
    {
        throw new NotImplementedException();
    }


    internal async Task StartMainExecuteWorkflowAsync(TriggerWorkflowRequestDto request, string blockId)
    {
        try
        {
            await this.ExecuteWorkflowAsync(request, blockId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Block '{blockId}': 工作流执行失败。");
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
        List<string> streamChunks = []; // 存储流式块以便最终组合

        try
        {
            // === 模拟 DeepFake 流式输出 ===
            var fakeResponseParts = new List<string>
            {
                /* 保持你的 DeepFake 文本列表 */
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
                var updateDto = new DisplayUpdateDto(RequestId: request.RequestId, // 关联请求
                    ContextBlockId: blockId, StreamingStatus: StreamStatus.Streaming, Content: part, // 发送当前的文本片段
                    UpdateMode: UpdateMode.FullSnapshot);
                // 使用 NotifierService 发送 (确保 NotifierService 已实现 SendWorkflowUpdate)
                // 注意：这里需要实际调用 INotifierService 的方法，我们假设它存在并能广播
                await this.notifierService.NotifyDisplayUpdateAsync(updateDto); // <--- 添加此行
                streamChunks.Add(part); // 存储块

                // 安全地截取日志摘要
                Log.Debug(
                    $"Block '{blockId}': Workflow sent stream chunk: '{part.Substring(0, Math.Min(part.Length, 20))}...'");
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(80, 300))); // 模拟延迟
            }

            // === 模拟指令生成 ===
            // (这里我们不再从 DeepFake 文本中解析，而是直接写死一些指令)
            if (request.Params.TryGetValue("create_item_id", out object? itemIdObj) && itemIdObj is string itemId)
            {
                generatedCommands.Add(AtomicOperation.Create(EntityType.Item, itemId,
                    new() { { "name", $"Item {itemId} (from workflow)" }, { "created_by", request.WorkflowName } }));
                Log.Debug($"Block '{blockId}': Workflow generated command to create item {itemId}.");
            }

            // 假设需要创建 DeepFake 提到的地点和角色 (如果它们在 WsInput 不存在)
            // 注意：实际应用中需要检查是否存在，避免重复创建错误
            generatedCommands.Add(AtomicOperation.Create(EntityType.Place, "castle-entrance",
                new() { { "name", "城堡入口" } })); // 可能创建也可能覆盖
            generatedCommands.Add(AtomicOperation.Create(EntityType.Character, "clumsy-knight",
                new()
                {
                    { "name", "笨手笨脚的骑士" }, { "current_place", "Place:castle-entrance" }, { "hp", 15 },
                    { "description", "盔甲上全是凹痕，走路还同手同脚" }
                }));
            generatedCommands.Add(AtomicOperation.Create(EntityType.Item, "dropped-gauntlet",
                new() { { "name", "掉落的铁手套" }, { "location", "Place:castle-entrance" }, { "material", "生锈的铁" } }));
            generatedCommands.Add(AtomicOperation.Modify(EntityType.Place, "castle-entrance", "description", "+=",
                " 地上现在多了一只孤零零的铁手套和一个看起来不太聪明的骑士。"));


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
            await this.blockWritServices.HandleWorkflowCompletionAsync(blockId, request.RequestId, success,
                rawTextResult, generatedCommands, outputVariables);
            Log.Debug($"Block '{blockId}': 已通知 BlockManager 工作流完成状态: Success={success}");

            // 发送最终完成状态 (如果需要单独通知)
            var completeDto = new DisplayUpdateDto(RequestId: request.RequestId, ContextBlockId: blockId,
                StreamingStatus: success ? StreamStatus.Complete : StreamStatus.Error, Content: rawTextResult,
                UpdateMode: UpdateMode.FullSnapshot);
            await this.notifierService.NotifyDisplayUpdateAsync(completeDto); // <--- 添加此行
        }
    }

    public async Task HandleConflictResolutionAsync(ResolveConflictRequestDto request)
    {
        Log.Info($"收到冲突解决请求: RequestId={request.RequestId}, BlockId={request.BlockId}");

        await this.blockWritServices.ApplyResolvedCommandsAsync(request.BlockId, request.ResolvedCommands);

        Log.Info($"Block '{request.BlockId}': 已提交冲突解决方案。");
    }
}
// --- END OF FILE WorkflowService.cs ---