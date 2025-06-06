﻿// --- START OF FILE WorkflowService.cs ---

using System.Text.Json;
using YAESandBox.Core.Action;
using YAESandBox.Core.DTOs;
using YAESandBox.Core.DTOs.WebSocket;
using YAESandBox.Core.Services.InterFaceAndBasic;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;
using YAESandBox.Workflow.Abstractions;

// For serializing params

namespace YAESandBox.Core.Services.WorkFlow;

/// <summary>
/// 工作流服务
/// </summary>
/// <param name="blockServices"></param>
/// <param name="notifierService"></param>
public class WorkflowService(
    IWorkFlowBlockService blockServices,
    IWorkflowNotifierService notifierService
) : IWorkflowService
{
    private IWorkFlowBlockService BlockServices { get; } = blockServices;
    private IWorkflowNotifierService NotifierService { get; } = notifierService;
    // private readonly IAiService _aiService; // 未来可能注入 AI 服务

    ///<inheritdoc/>
    public async Task HandleMainWorkflowTriggerAsync(TriggerMainWorkflowRequestDto request)
    {
        Log.Info(
            $"WorkflowService: 收到主工作流触发请求: RequestId={request.RequestId}, Workflow={request.WorkflowName}, ParentBlock={request.ParentBlockId}");
        var childBlock =
            await this.BlockServices.CreateChildBlockAsync(request.ParentBlockId, request.WorkflowName, request.Params);
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

    /// <inheritdoc/>
    public async Task HandleRegenerateBlockAsync(RegenerateBlockRequestDto request)
    {
        Log.Info($"WorkflowService: 处理重新生成请求: RequestId={request.RequestId}, BlockId={request.BlockId}");
        // 1. 尝试将 Block 置于 Loading 状态
        //    注意：这里调用的是 BlockWritService 的方法，它会负责调用 BlockManager 并发送初始通知
        var loadingStatusResult = await this.BlockServices.TryStartRegenerationAsync(request.BlockId);

        if (!loadingStatusResult.TryGetValue(out var loadingStatus))
        {
            // 启动失败，BlockWritService 或 BlockManager 已记录日志
            // 可能需要通知调用者失败？目前仅记录日志
            Log.Warning($"WorkflowService: 无法为 Block '{request.BlockId}' 启动重新生成流程 (可能状态不对或不存在)。");
            // 可以在这里发送一个 DisplayUpdateDto(StreamStatus=Error) 给请求者吗？
            // 这比较复杂，因为没有 TargetElementId，且主流程 DisplayUpdate 通常与 Block 相关
            // 暂时不额外通知，依赖 BlockWritService 发送的状态更新（如果状态变为 Loading 的话）
            return;
        }

        var block = loadingStatus.Block;
        var newRequest = new TriggerMainWorkflowRequestDto()
        {
            RequestId = request.RequestId,
            WorkflowName = block.WorkflowName,
            ParentBlockId = request.BlockId,
            Params = block.TriggeredParams
        };

        Log.Info($"WorkflowService: Block '{request.BlockId}' 已成功进入 Loading 状态，准备执行重新生成工作流 '{block.WorkflowName}'。");

        // 2. 异步执行工作流逻辑 (使用 Task.Run)
        //    我们将创建一个新的执行方法或重用/改造 ExecuteMainWorkflowAsync
        _ = Task.Run<Task>(() => this.StartMainExecuteWorkflowAsync(newRequest, block.BlockId)); // 传入 Block ID
    }

    ///<inheritdoc/>
    public Task HandleMicroWorkflowTriggerAsync(TriggerMicroWorkflowRequestDto request)
    {
        Log.Info(
            $"WorkflowService: 收到微工作流触发请求: RequestId={request.RequestId}, Workflow={request.WorkflowName}, TargetElementId={request.TargetElementId}, ContextBlockId={request.ContextBlockId}");

        // 微工作流不创建 Block，直接在后台执行
        // 异步执行模拟逻辑
        _ = Task.Run(() => this.StartMicroExecuteWorkflowAsync(request));

        // 立即返回
        return Task.CompletedTask;
    }

    // 为了方便测试而分离出来
    internal async Task StartMainExecuteWorkflowAsync(TriggerMainWorkflowRequestDto request, string blockId)
    {
        try
        {
            await this.ExecuteMainWorkflowAsync(request, blockId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Block '{blockId}': 工作流执行失败。");
        }
    }

    // 为了方便测试而分离出来
    internal async Task StartMicroExecuteWorkflowAsync(TriggerMicroWorkflowRequestDto request)
    {
        try
        {
            await this.ExecuteMicroWorkflowAsync(request);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Request '{request.RequestId}': 工作流执行失败。");
        }
    }

    private async Task ExecuteMicroWorkflowAsync(TriggerMicroWorkflowRequestDto request)
    {
        Log.Debug(
            $"开始后台执行微工作流 '{request.WorkflowName}' for TargetElementId '{request.TargetElementId}' on ContextBlockId '{request.ContextBlockId}'...");
        bool success = false;
        string finalMessage = ""; // 存储最终发送的消息

        try
        {
            // === 模拟微工作流工作（例如获取建议、计算信息） ===
            var simulatedContent = new List<string>
            {
                $"[微工作流 '{request.WorkflowName}'] 正在处理...\n",
                $"请求参数: {JsonSerializer.Serialize(request.Params)}\n",
                "思考中... (模拟延迟)...\n",
                "也许你可以尝试... (生成建议中)...\n",
                $"建议 1: 检查一下 '{request.ContextBlockId}' 的元数据。\n",
                $"建议 2: 看看能否对 '{request.ContextBlockId}' 应用 'Modify' 操作。\n",
                $"最终建议: 喝杯咖啡休息一下！☕\n",
                $"[微工作流 '{request.WorkflowName}'] 处理完毕。\n"
            };

            // === 模拟流式输出到特定 UI 元素 ===
            foreach (string part in simulatedContent)
            {
                var updateDto = new DisplayUpdateDto(
                    request.RequestId, // 关联请求
                    request.ContextBlockId, // 上下文 Block
                    part, // 当前内容片段
                    StreamStatus.Streaming,
                    UpdateMode.Incremental // 微工作流内容通常也是增量
                )
                {
                    // *** 关键：设置 TargetElementId ***
                    TargetElementId = request.TargetElementId
                    // ScriptId 通常不需要为微工作流设置
                };
                await this.NotifierService.NotifyDisplayUpdateAsync(updateDto);

                Log.Debug(
                    $"微工作流 '{request.WorkflowName}' 发送流片段到 '{request.TargetElementId}': '{part.Substring(0, Math.Min(part.Length, 50))}...'");
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(40, 120))); // 模拟延迟
            }

            success = true;
            finalMessage = $"[微工作流 '{request.WorkflowName}' 成功完成]";
            Log.Info($"微工作流 '{request.WorkflowName}' for TargetElementId '{request.TargetElementId}' 模拟执行成功。");
        }
        catch (Exception ex)
        {
            success = false;
            finalMessage = $"[微工作流 '{request.WorkflowName}' 执行失败: {ex.Message}]";
            Log.Error(ex,
                $"微工作流 '{request.WorkflowName}' for TargetElementId '{request.TargetElementId}' 执行过程中发生异常: {ex.Message}");
        }
        finally
        {
            // *** 微工作流完成：发送最终状态通知 ***
            // 不需要调用 HandleWorkflowCompletionAsync
            var finalStatus = success ? StreamStatus.Complete : StreamStatus.Error;
            var completeDto = new DisplayUpdateDto(
                request.RequestId,
                request.ContextBlockId,
                finalMessage, // 最终消息
                finalStatus // 完成或错误状态
            )
            {
                TargetElementId = request.TargetElementId // 确保最终状态也发往目标元素
            };
            await this.NotifierService.NotifyDisplayUpdateAsync(completeDto);
            Log.Debug(
                $"微工作流 '{request.WorkflowName}' for TargetElementId '{request.TargetElementId}' 发送最终状态通知: {finalStatus}");
        }
        // 微工作流的 Task.Run 级别的异常处理与主工作流类似，但通知 DTO 需要包含 TargetElementId
        // catch (Exception ex) { ... Log ... Send panic DTO with TargetElementId ... }
    }

    // 实际执行工作流的私有方法
    private async Task ExecuteMainWorkflowAsync(TriggerMainWorkflowRequestDto request, string blockId)
    {
        Log.Debug($"Block '{blockId}': 开始执行工作流 '{request.WorkflowName}'...");
        bool success;
        string rawTextResult;
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

            foreach (string part in fakeResponseParts)
            {
                // *** 发送流式更新到前端 ***
                var updateDto = new DisplayUpdateDto(request.RequestId, // 关联请求
                    blockId, StreamingStatus: StreamStatus.Streaming, Content: part, // 发送当前的文本片段
                    UpdateMode: UpdateMode.FullSnapshot);
                // 使用 NotifierService 发送 (确保 NotifierService 已实现 SendWorkflowUpdate)
                // 注意：这里需要实际调用 INotifierService 的方法，我们假设它存在并能广播
                await this.NotifierService.NotifyDisplayUpdateAsync(updateDto); // <--- 添加此行
                streamChunks.Add(part); // 存储块

                // 安全地截取日志摘要
                Log.Debug(
                    $"Block '{blockId}': Workflow sent stream chunk: '{part.Substring(0, Math.Min(part.Length, 20))}...'");
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(8, 30))); // 模拟延迟
            }

            // === 模拟指令生成 ===

            // 假设需要创建 DeepFake 提到的地点和角色 (如果它们在 WsInput 不存在)
            // 注意：实际应用中需要检查是否存在，避免重复创建错误
            generatedCommands.Add(AtomicOperation.Create(EntityType.Place, "castle-entrance",
                new Dictionary<string, object?> { { "name", "城堡入口" } })); // 可能创建也可能覆盖
            generatedCommands.Add(AtomicOperation.Create(EntityType.Character, "clumsy-knight",
                new Dictionary<string, object?>
                {
                    { "name", "笨手笨脚的骑士" }, { "current_place", "Place:castle-entrance" }, { "hp", 15 },
                    { "description", "盔甲上全是凹痕，走路还同手同脚" }
                }));
            generatedCommands.Add(AtomicOperation.Create(EntityType.Item, "dropped-gauntlet",
                new Dictionary<string, object?> { { "name", "掉落的铁手套" }, { "location", "Place:castle-entrance" }, { "material", "生锈的铁" } }));
            generatedCommands.Add(AtomicOperation.Modify(EntityType.Place, "castle-entrance", "description", "+=",
                " 地上现在多了一只孤零零的铁手套和一个看起来不太聪明的骑士。"));


            // === 格式化最终的 rawText (可以包含所有流式块或其他信息) ===
            rawTextResult = string.Join("", streamChunks);
            //     JsonSerializer.Serialize(new
            // {
            //     workflowName = request.WorkflowName,
            //     fullStreamedContent = string.Join("", streamChunks), // 合并所有流式块
            //     finalNote = "工作流执行完毕 (模拟)。",
            //     // 可以添加其他需要持久化的信息
            // }, new JsonSerializerOptions { WriteIndented = true });

            success = true;
            Log.Info($"Block '{blockId}': 工作流 '{request.WorkflowName}' 执行成功。");
        }
        catch (Exception ex)
        {
            success = false;
            rawTextResult = $"工作流 '{request.WorkflowName}' 执行失败: {ex.Message}";
            Log.Error(ex, $"Block '{blockId}': 工作流 '{request.WorkflowName}' 执行过程中发生异常: {ex.Message}");
        }

        var blockResult = await this.BlockServices.HandleWorkflowCompletionAsync(blockId, request.RequestId,
            success, rawTextResult, generatedCommands.ToAtomicOperationRequests(), outputVariables);

        Log.Debug($"Block '{blockId}': 已通知 BlockManager 工作流完成状态: Success={success}");

        // 发送最终完成状态 (如果需要单独通知)
        var completeDto = new DisplayUpdateDto(request.RequestId, blockId,
            StreamingStatus: success ? StreamStatus.Complete : StreamStatus.Error, Content: rawTextResult,
            UpdateMode: UpdateMode.FullSnapshot);
        await this.NotifierService.NotifyDisplayUpdateAsync(completeDto);
    }

    ///<inheritdoc/>
    public async Task HandleConflictResolutionAsync(ResolveConflictRequestDto request)
    {
        Log.Info($"收到冲突解决请求: RequestId={request.RequestId}, BlockId={request.BlockId}");

        await this.BlockServices.ApplyResolvedCommandsAsync(
            request.BlockId,
            request.ResolvedCommands);

        Log.Info($"Block '{request.BlockId}': 已提交冲突解决方案。");
    }
}
// --- END OF FILE WorkflowService.cs ---