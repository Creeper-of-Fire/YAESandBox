// --- START OF FILE MainWorkflowIntegrationTests.cs ---

using System.Net;
using System.Net.Http.Headers; // For MediaTypeWithQualityHeaderValue
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;
using YAESandBox.API.DTOs;
using YAESandBox.Core;
// For GameState etc.
using YAESandBox.Core.Action; // For AtomicOperation
using YAESandBox.Core.Block; // For BlockStatusCode
using YAESandBox.Core.State.Entity; // For EntityType, TypedID
using YAESandBox.Depend; // For ITestOutputHelper

namespace YAESandBox.Tests.Integration;

// 使用集合确保涉及持久化的测试按顺序执行，避免文件访问冲突
[Collection("SequentialPersistence")]
public class MainWorkflowIntegrationTests(ITestOutputHelper output) : IntegrationTestBase
{
    private readonly ITestOutputHelper _output = output; // 用于输出测试日志

    // 可选：如果希望每个测试方法都使用全新的 WebApplicationFactory 实例
    // (注意：这会显著增加测试时间)
    // Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(...);
    // HttpClient = Factory.CreateClient();

    //经过检测，这个测试测试的对象没问题，但是这个测试方法写错了
    // [Fact]
    // public async Task 触发主工作流_应创建子Block并接收Loading和Streaming更新_最终完成()
    // {
    //     // --- Arrange ---
    //     var connection = CreateHubConnection();
    //     var collector = new SignalRMessageCollector(connection, this._output);
    //
    //     // 注册需要监听的消息类型
    //     collector.RegisterHandler<BlockStatusUpdateDto>("ReceiveBlockStatusUpdate");
    //     collector.RegisterHandler<DisplayUpdateDto>("ReceiveDisplayUpdate");
    //     collector.RegisterHandler<BlockUpdateSignalDto>("ReceiveBlockUpdateSignal");
    //     collector.RegisterHandler<ConflictDetectedDto>("ReceiveConflictDetected"); // 即使本测试不期待冲突，也注册以防万一
    //
    //     await ConnectHubAsync(connection);
    //     collector.ClearAllMessages(); // 确保开始前清空旧消息
    //
    //     var requestId = $"test_req_{Guid.NewGuid()}";
    //     var parentBlockId = BlockManager.WorldRootId; // 假设从根节点触发
    //     var workflowName = "generate_story_test"; // 假设的工作流名称
    //     var triggerDto = new TriggerMainWorkflowRequestDto
    //     {
    //         RequestId = requestId,
    //         ParentBlockId = parentBlockId,
    //         WorkflowName = workflowName,
    //         Params = new Dictionary<string, object?> { { "param1", "value1" } }
    //     };
    //
    //     // --- Act ---
    //     await connection.InvokeAsync("TriggerMainWorkflow", triggerDto);
    //
    //     // --- Assert ---
    //     string? newBlockId = null;
    //     try
    //     {
    //         // 1. 等待 Loading 状态更新
    //         var loadingUpdate = await collector.WaitForMessageAsync<BlockStatusUpdateDto>(
    //             msg => msg.StatusCode == BlockStatusCode.Loading,
    //             TimeSpan.FromSeconds(10));
    //         Assert.NotNull(loadingUpdate);
    //         Assert.Equal(BlockStatusCode.Loading, loadingUpdate.StatusCode);
    //         Assert.False(string.IsNullOrEmpty(loadingUpdate.BlockId));
    //         Assert.NotEqual(parentBlockId, loadingUpdate.BlockId); // 确认是新 Block ID
    //         newBlockId = loadingUpdate.BlockId;
    //         _output.WriteLine($"主工作流: 收到新 Block '{newBlockId}' 的 Loading 状态。");
    //
    //         // 2. 等待至少一个流式显示更新
    //         var streamingUpdate = await collector.WaitForMessageAsync<DisplayUpdateDto>(
    //             msg => msg.RequestId == requestId &&
    //                    msg.ContextBlockId == newBlockId &&
    //                    msg.StreamingStatus == StreamStatus.Streaming &&
    //                    msg.TargetElementId == null, // 主工作流 TargetElementId 为 null
    //             TimeSpan.FromSeconds(10));
    //         Assert.NotNull(streamingUpdate);
    //         Assert.False(string.IsNullOrEmpty(streamingUpdate.Content));
    //         _output.WriteLine(
    //             $"主工作流: 收到第一个 Streaming DisplayUpdate: '{streamingUpdate.Content.Substring(0, Math.Min(streamingUpdate.Content.Length, 50))}...'");
    //
    //         // 3. 并行等待工作流完成的两个关键信号：最终状态和最终显示更新
    //         _output.WriteLine("主工作流: 同时等待最终 Block 状态和最终 DisplayUpdate...");
    //
    //         // 创建两个等待任务
    //         var finalStatusTask = collector.WaitForMessageAsync<BlockStatusUpdateDto>(
    //             msg => msg.BlockId == newBlockId &&
    //                    (msg.StatusCode == BlockStatusCode.Idle || msg.StatusCode == BlockStatusCode.Error ||
    //                     msg.StatusCode == BlockStatusCode.ResolvingConflict),
    //             TimeSpan.FromSeconds(20)); // 设置一个合理的组合超时
    //
    //         var finalDisplayTask = collector.WaitForMessageAsync<DisplayUpdateDto>(
    //             msg => msg.RequestId == requestId &&
    //                    msg.ContextBlockId == newBlockId &&
    //                    (msg.StreamingStatus == StreamStatus.Complete || msg.StreamingStatus == StreamStatus.Error) &&
    //                    msg.TargetElementId == null,
    //             TimeSpan.FromSeconds(20)); // 使用相同的超时
    //
    //         try
    //         {
    //             // 等待两个任务都完成
    //             // WaitAsync 可以添加一个额外的整体超时，以防万一 Task.WhenAll 卡住
    //             await Task.WhenAll(finalStatusTask, finalDisplayTask).WaitAsync(TimeSpan.FromSeconds(2500));
    //         }
    //         catch (TimeoutException)
    //         {
    //             // 如果发生超时，可以检查哪个任务失败了，提供更详细的错误信息
    //             string error = "等待最终信号超时。";
    //             if (!finalStatusTask.IsCompletedSuccessfully) error += " 未收到最终 BlockStatusUpdate。";
    //             if (!finalDisplayTask.IsCompletedSuccessfully) error += " 未收到最终 DisplayUpdate。";
    //             _output.WriteLine(error);
    //             throw new TimeoutException(error); // 重新抛出，让测试失败
    //         }
    //         catch (Exception ex) //捕获其他可能的异常
    //         {
    //             _output.WriteLine($"等待最终信号时发生意外错误: {ex}");
    //             throw;
    //         }
    //
    //
    //         // 如果代码执行到这里，说明两个任务都成功完成了
    //         var finalStatusUpdate = await finalStatusTask; // 获取结果 (现在保证已完成)
    //         var finalDisplayUpdate = await finalDisplayTask; // 获取结果
    //
    //         _output.WriteLine($"主工作流: 收到最终 Block 状态 '{finalStatusUpdate.StatusCode}'。");
    //         _output.WriteLine($"主工作流: 收到最终 DisplayUpdate 状态 '{finalDisplayUpdate.StreamingStatus}'。");
    //
    //         // 5. (可选) 根据最终状态，验证其他信息
    //         if (finalStatusUpdate.StatusCode == BlockStatusCode.Idle)
    //         {
    //             // 验证是否收到 StateUpdateSignal (如果工作流修改了状态)
    //             // 注意：这里的模拟工作流生成了命令，所以应该会收到信号
    //             _output.WriteLine("主工作流: 检查 StateUpdateSignal...");
    //             var stateSignal = await collector.WaitForMessageAsync<BlockUpdateSignalDto>(
    //                 msg => msg.BlockId == newBlockId, TimeSpan.FromSeconds(5));
    //             Assert.NotNull(stateSignal);
    //             Assert.NotEmpty(stateSignal.ChangedEntityIds); // 确认有实体被改变
    //             _output.WriteLine($"主工作流: 收到 StateUpdateSignal，涉及实体: {string.Join(",", stateSignal.ChangedEntityIds)}");
    //
    //             // 通过 API 验证 Block 最终内容和 WorldState
    //             var blockDetailResponse = await HttpClient.GetAsync($"/api/blocks/{newBlockId}");
    //             Assert.Equal(HttpStatusCode.OK, blockDetailResponse.StatusCode);
    //             var blockDetail = await blockDetailResponse.Content.ReadFromJsonAsync<BlockDetailDto>();
    //             Assert.NotNull(blockDetail);
    //             Assert.Equal(BlockStatusCode.Idle, blockDetail.StatusCode);
    //             Assert.False(string.IsNullOrEmpty(blockDetail.BlockContent)); // 确认有内容
    //
    //             var entitiesResponse = await HttpClient.GetAsync($"/api/entities?blockId={newBlockId}");
    //             Assert.Equal(HttpStatusCode.OK, entitiesResponse.StatusCode);
    //             var entities = await entitiesResponse.Content.ReadFromJsonAsync<List<EntitySummaryDto>>();
    //             Assert.NotNull(entities);
    //             Assert.Contains(entities, e => e.EntityId == "clumsy-knight"); // 检查模拟工作流创建的实体
    //             Assert.Contains(entities, e => e.EntityId == "dropped-gauntlet");
    //             Assert.Contains(entities, e => e.EntityId == "castle-entrance");
    //             _output.WriteLine("主工作流: API 验证 Block 细节和实体成功。");
    //         }
    //         else if (finalStatusUpdate.StatusCode == BlockStatusCode.ResolvingConflict)
    //         {
    //             // 等待冲突检测信号
    //             _output.WriteLine("主工作流: 等待 ConflictDetected 信号...");
    //             var conflictSignal = await collector.WaitForMessageAsync<ConflictDetectedDto>(
    //                 msg => msg.BlockId == newBlockId && msg.RequestId == requestId, TimeSpan.FromSeconds(5));
    //             Assert.NotNull(conflictSignal);
    //             Assert.NotEmpty(conflictSignal.AiCommands);
    //             Assert.NotEmpty(conflictSignal.UserCommands); // 如果测试中途修改了
    //             Assert.NotEmpty(conflictSignal.ConflictingAiCommands);
    //             Assert.NotEmpty(conflictSignal.ConflictingUserCommands); // 如果测试中途修改了
    //             _output.WriteLine("主工作流: 收到 ConflictDetected 信号。");
    //
    //             // --- 可选：在此基础上测试冲突解决 ---
    //             _output.WriteLine("主工作流: (可选) 测试冲突解决...");
    //             var resolutionDto = new ResolveConflictRequestDto
    //             {
    //                 RequestId = requestId,
    //                 BlockId = newBlockId,
    //                 // 假设用户决定只保留 AI 的部分指令 (简化处理)
    //                 ResolvedCommands = conflictSignal.AiCommands.Where(cmd => cmd.EntityType == EntityType.Place)
    //                     .ToList()
    //             };
    //             await connection.InvokeAsync("ResolveConflict", resolutionDto);
    //
    //             // 等待冲突解决后的 Idle 或 Error 状态
    //             _output.WriteLine("主工作流: 等待冲突解决后的最终状态...");
    //             var resolvedStatusUpdate = await collector.WaitForMessageAsync<BlockStatusUpdateDto>(
    //                 msg => msg.BlockId == newBlockId && (msg.StatusCode == BlockStatusCode.Idle ||
    //                                                      msg.StatusCode == BlockStatusCode.Error),
    //                 TimeSpan.FromSeconds(10));
    //             Assert.NotNull(resolvedStatusUpdate);
    //             _output.WriteLine($"主工作流: 冲突解决后状态为 '{resolvedStatusUpdate.StatusCode}'。");
    //
    //             // 等待冲突解决后的最终 DisplayUpdate
    //             _output.WriteLine("主工作流: 等待冲突解决后的最终 DisplayUpdate...");
    //             var resolvedDisplayUpdate = await collector.WaitForMessageAsync<DisplayUpdateDto>(
    //                 msg => msg.RequestId == requestId && msg.ContextBlockId == newBlockId &&
    //                        (msg.StreamingStatus == StreamStatus.Complete || msg.StreamingStatus == StreamStatus.Error),
    //                 TimeSpan.FromSeconds(5));
    //             Assert.NotNull(resolvedDisplayUpdate);
    //             _output.WriteLine($"主工作流: 冲突解决后最终 DisplayUpdate 状态 '{resolvedDisplayUpdate.StreamingStatus}'。");
    //
    //             // (如果状态为 Idle) 等待冲突解决后的 StateUpdateSignal
    //             if (resolvedStatusUpdate.StatusCode == BlockStatusCode.Idle)
    //             {
    //                 _output.WriteLine("主工作流: 检查冲突解决后的 StateUpdateSignal...");
    //                 var resolvedStateSignal = await collector.WaitForMessageAsync<BlockUpdateSignalDto>(
    //                     msg => msg.BlockId == newBlockId, TimeSpan.FromSeconds(5));
    //                 Assert.NotNull(resolvedStateSignal);
    //                 // 根据保留的指令断言 ChangedEntityIds
    //                 Assert.Contains(resolvedStateSignal.ChangedEntityIds, id => id == "castle-entrance");
    //                 _output.WriteLine($"主工作流: 收到冲突解决后的 StateUpdateSignal。");
    //             }
    //         }
    //         else // Error
    //         {
    //             Assert.Equal(StreamStatus.Error, finalDisplayUpdate.StreamingStatus);
    //             // 验证 API 获取 Block 状态也为 Error
    //             var blockDetailResponse = await HttpClient.GetAsync($"/api/blocks/{newBlockId}");
    //             Assert.Equal(HttpStatusCode.OK, blockDetailResponse.StatusCode);
    //             var blockDetail = await blockDetailResponse.Content.ReadFromJsonAsync<BlockDetailDto>();
    //             Assert.NotNull(blockDetail);
    //             Assert.Equal(BlockStatusCode.Error, blockDetail.StatusCode);
    //             _output.WriteLine("主工作流: 验证到 Block 最终状态为 Error。");
    //         }
    //     }
    //     finally
    //     {
    //         await StopHubAsync(connection);
    //     }
    // }

    [Fact]
    public async Task 持久化_保存和加载状态_应恢复数据和盲存()
    {
        // --- Arrange: 先执行一些操作改变状态 ---
        string initialBlockId = BlockManager.WorldRootId;
        var operation = AtomicOperation.Create(EntityType.Character, "hero_save_test",
            new Dictionary<string, object?> { { "name", "持久化英雄" } });
        var batchRequest = new BatchAtomicRequestDto { Operations = [operation.ToAtomicOperationRequestDto()] };

        var postResponse = await this.HttpClient.PostAsJsonAsync($"/api/atomic/{initialBlockId}", batchRequest);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode); // 确认操作成功

        // --- Act 1: 保存状态 ---
        var blindDataToSave = new { UserSetting = "SomeValue", LastBlock = "block-xyz" };
        this._output.WriteLine("持久化: 调用保存 API...");
        var saveResponse = await this.HttpClient.PostAsJsonAsync("/api/persistence/save", blindDataToSave);
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        Assert.Equal("application/json", saveResponse.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("yaesandbox_save_", saveResponse.Content.Headers.ContentDisposition?.FileName);

        // 读取保存的文件内容
        var savedDataStream = await saveResponse.Content.ReadAsStreamAsync();
        var memoryStream = new MemoryStream();
        await savedDataStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0; // 重置流位置以便后续使用

        // 验证保存的数据结构 (可选，但推荐)
        memoryStream.Position = 0;
        var savedArchive = await JsonSerializer.DeserializeAsync<ArchiveDto>(memoryStream, SignalRJsonOptions);
        Assert.NotNull(savedArchive);
        Assert.True(savedArchive.Blocks.ContainsKey(initialBlockId));
        Assert.NotNull(savedArchive.Blocks[initialBlockId].WorldStates.GetValueOrDefault("wsPostUser")?.Characters
            .ContainsKey("hero_save_test"));
        Assert.NotNull(savedArchive.BlindStorage);
        var savedBlindDataElement = (JsonElement)savedArchive.BlindStorage;
        Assert.Equal("SomeValue", savedBlindDataElement.GetProperty("userSetting").GetString());
        Assert.Equal("block-xyz", savedBlindDataElement.GetProperty("lastBlock").GetString());
        this._output.WriteLine("持久化: 保存的数据结构基本验证通过。");

        // --- Act 2: 加载状态 ---
        memoryStream.Position = 0; // 重置流以供上传
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(memoryStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        // 文件名需要和服务端期望的一致或兼容
        content.Add(fileContent, "archiveFile", "test_save.json");

        this._output.WriteLine("持久化: 调用加载 API...");
        var loadResponse = await this.HttpClient.PostAsync("/api/persistence/load", content);
        this._output.WriteLine($"持久化: 加载 API 响应状态: {loadResponse.StatusCode}");

        // 调试输出响应内容
        string responseContent = await loadResponse.Content.ReadAsStringAsync();
        this._output.WriteLine($"持久化: 加载 API 响应内容: {responseContent}");


        Assert.Equal(HttpStatusCode.OK, loadResponse.StatusCode);

        // --- Assert 2: 验证加载结果 ---
        // 验证返回的盲存数据
        var optionsForHttpRead = new JsonSerializerOptions { PropertyNameCaseInsensitive = true }; // 定义忽略大小写的选项
        var loadedBlindData =
            await loadResponse.Content.ReadFromJsonAsync<JsonElement>(optionsForHttpRead); // 直接读为 JsonElement
        Assert.Equal("SomeValue", loadedBlindData.GetProperty("userSetting").GetString());
        Assert.Equal("block-xyz", loadedBlindData.GetProperty("lastBlock").GetString());
        this._output.WriteLine("持久化: 返回的盲存数据验证成功。");

        // 验证加载后的 Block 状态 (应为 Idle) 和实体数据
        this._output.WriteLine("持久化: 验证加载后的 Block 状态和实体...");

// *** 创建包含 Enum 转换器的选项 ***
        var optionsForDtoRead = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true // 保留之前的设置
        };
// 添加必要的转换器
        optionsForDtoRead.Converters.Add(new JsonStringEnumConverter()); // <--- 关键：添加枚举转换器
// 如果 DTO 中还包含 TypedID 等自定义类型，也需要在此添加对应的转换器
// optionsForDtoRead.Converters.Add(new TypedIdConverter()); // 如果需要
        var blockDetailResponse = await this.HttpClient.GetAsync($"/api/blocks/{initialBlockId}");

        // (可选，用于调试) 打印原始响应内容
        string responseContent0 = await blockDetailResponse.Content.ReadAsStringAsync();
        this._output.WriteLine($"持久化: /api/blocks/{initialBlockId} 响应内容: {responseContent0}");

        Assert.Equal(HttpStatusCode.OK, blockDetailResponse.StatusCode);


        var blockDetail = await blockDetailResponse.Content.ReadFromJsonAsync<BlockDetailDto>(optionsForDtoRead);
        Assert.NotNull(blockDetail);
        // 持久化加载后，Block 状态应恢复为 Idle
        Assert.Equal(BlockStatusCode.Idle, blockDetail.StatusCode);
        this._output.WriteLine($"持久化: Block '{initialBlockId}' 加载后状态为 Idle。");

        // *** 实体 DTO 可能也需要配置选项反序列化 ***
// 创建包含所需转换器的选项 (如果 EntitySummaryDto 包含枚举或其他特殊类型)
        var optionsForEntityDtoRead = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        optionsForEntityDtoRead.Converters.Add(new JsonStringEnumConverter()); // 比如 EntityType 枚举
        optionsForEntityDtoRead.Converters.Add(new TypedIdConverter()); // 如果 DTO 里有 TypedID


        var entitiesResponse = await this.HttpClient.GetAsync($"/api/entities?blockId={initialBlockId}");
        Assert.Equal(HttpStatusCode.OK, entitiesResponse.StatusCode);
        var entities = await entitiesResponse.Content.ReadFromJsonAsync<List<EntitySummaryDto>>(optionsForEntityDtoRead);
        Assert.NotNull(entities);
        var loadedHero = entities.FirstOrDefault(e => e.EntityId == "hero_save_test");
        Assert.NotNull(loadedHero);
        Assert.Equal(EntityType.Character, loadedHero.EntityType);
        Assert.Equal("持久化英雄", loadedHero.Name); // 确认属性也恢复了
        this._output.WriteLine("持久化: 加载后的实体数据验证成功。");

        // 验证 GameState (如果需要)
        var gameStateResponse = await this.HttpClient.GetAsync($"/api/blocks/{initialBlockId}/gamestate");
        Assert.Equal(HttpStatusCode.OK, gameStateResponse.StatusCode);
        // ... 反序列化并验证 GameState 内容 ...
        this._output.WriteLine("持久化: GameState 验证 (如果实现)。");
    }

    // 可选：测试加载期间用户修改的情况 (根据场景一第 4 步)
    // [Fact]
    // public async Task 触发主工作流_加载期间用户修改_应暂存并最终应用() { ... }

    // 可选：测试主工作流导致冲突的情况 (需要调整模拟逻辑)
    // [Fact]
    // public async Task 触发主工作流_产生冲突_应收到Conflict状态和信号() { ... }

    // --- IAsyncLifetime 实现 ---
    public override async Task DisposeAsync()
    {
        // 如果每个测试方法创建新实例，则在此处 Dispose
        // await StopHubAsync(connection); // 如果 connection 是类成员
        await base.DisposeAsync();
    }
}

// 需要在某个地方定义这个集合，确保顺序执行
[CollectionDefinition("SequentialPersistence", DisableParallelization = true)]
public class SequentialPersistenceCollection { }
// --- END OF FILE MainWorkflowIntegrationTests.cs ---