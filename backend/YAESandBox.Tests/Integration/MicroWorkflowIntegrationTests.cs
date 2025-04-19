// --- START OF FILE MicroWorkflowIntegrationTests.cs ---

// For BlockStatusCode
using Xunit.Abstractions;

namespace YAESandBox.Tests.Integration;

public class MicroWorkflowIntegrationTests(ITestOutputHelper output) : IntegrationTestBase
{
     private readonly ITestOutputHelper _output = output;

//      [Fact]
//     public async Task 触发微工作流_应接收针对特定元素的Streaming和Complete更新_且不改变Block状态()
//     {
//         // --- Arrange ---
//         var connection = CreateHubConnection();
//         var collector = new SignalRMessageCollector(connection,this._output);
//
//         // 只需要监听 DisplayUpdate，以及可选的 BlockStatusUpdate/StateUpdateSignal 用于验证 *没有* 收到
//         collector.RegisterHandler<DisplayUpdateDto>("ReceiveWorkflowUpdate");
//         collector.RegisterHandler<BlockStatusUpdateDto>("ReceiveBlockStatusUpdate");
//         collector.RegisterHandler<StateUpdateSignalDto>("ReceiveStateUpdateSignal");
//
//         await ConnectHubAsync(connection);
//         collector.ClearAllMessages();
//
//         var requestId = $"test_micro_req_{Guid.NewGuid()}";
//         var contextBlockId = BlockManager.WorldRootId; // 微工作流通常需要一个上下文 Block ID
//         var targetElementId = "suggestion-box-123"; // 目标 UI 元素
//         var workflowName = "generate_suggestion_test";
//         var triggerDto = new TriggerMicroWorkflowRequestDto
//         {
//             RequestId = requestId,
//             ContextBlockId = contextBlockId,
//             TargetElementId = targetElementId,
//             WorkflowName = workflowName,
//             Params = new Dictionary<string, object?> { { "topic", "greeting" } }
//         };
//         
//         // *** 创建包含 Enum 转换器的选项 ***
//         var optionsForDtoRead = new JsonSerializerOptions
//         {
//             PropertyNameCaseInsensitive = true // 保留之前的设置
//         };
// // 添加必要的转换器
//         optionsForDtoRead.Converters.Add(new JsonStringEnumConverter()); // <--- 关键：添加枚举转换器
//         
//         // *** 实体 DTO 可能也需要配置选项反序列化 ***
// // 创建包含所需转换器的选项 (如果 EntitySummaryDto 包含枚举或其他特殊类型)
//         var optionsForEntityDtoRead = new JsonSerializerOptions
//         {
//             PropertyNameCaseInsensitive = true
//         };
//         optionsForEntityDtoRead.Converters.Add(new JsonStringEnumConverter()); // 比如 EntityType 枚举
//         optionsForEntityDtoRead.Converters.Add(new TypedIdConverter()); // 如果 DTO 里有 TypedID
//
//          // 获取触发前的 Block 状态 (可选)
//         var initialBlockResponse = await HttpClient.GetAsync($"/api/blocks/{contextBlockId}");
//         Assert.Equal(HttpStatusCode.OK, initialBlockResponse.StatusCode);
//         var initialBlockDetail = await initialBlockResponse.Content.ReadFromJsonAsync<BlockDetailDto>(optionsForDtoRead);
//         Assert.NotNull(initialBlockDetail);
//          _output.WriteLine($"微工作流: 触发前 Block '{contextBlockId}' 状态为 {initialBlockDetail.StatusCode}。");
//
//         // --- Act ---
//         await connection.InvokeAsync("TriggerMicroWorkflow", triggerDto);
//
//         // --- Assert ---
//         try
//         {
//             // 1. 等待至少一个流式显示更新，并验证 TargetElementId
//             _output.WriteLine($"微工作流: 等待第一个 Streaming DisplayUpdate for TargetElementId '{targetElementId}'...");
//             var streamingUpdate = await collector.WaitForMessageAsync<DisplayUpdateDto>(
//                 msg => msg.RequestId == requestId &&
//                        msg.ContextBlockId == contextBlockId &&
//                        msg.TargetElementId == targetElementId && // 关键：检查 TargetElementId
//                        msg.StreamingStatus == StreamStatus.Streaming,
//                 TimeSpan.FromSeconds(10));
//             Assert.NotNull(streamingUpdate);
//             Assert.False(string.IsNullOrEmpty(streamingUpdate.Content));
//              _output.WriteLine($"微工作流: 收到 Streaming DisplayUpdate: '{streamingUpdate.Content.Substring(0, Math.Min(streamingUpdate.Content.Length, 50))}...'");
//
//
//             // 2. 等待最终的完成/错误显示更新，并验证 TargetElementId
//              _output.WriteLine($"微工作流: 等待最终 DisplayUpdate for TargetElementId '{targetElementId}'...");
//             var finalDisplayUpdate = await collector.WaitForMessageAsync<DisplayUpdateDto>(
//                 msg => msg.RequestId == requestId &&
//                        msg.ContextBlockId == contextBlockId &&
//                        msg.TargetElementId == targetElementId && // 关键：再次检查 TargetElementId
//                        (msg.StreamingStatus == StreamStatus.Complete || msg.StreamingStatus == StreamStatus.Error),
//                 TimeSpan.FromSeconds(15)); // 微工作流可能也需要一些时间
//             Assert.NotNull(finalDisplayUpdate);
//             Assert.False(string.IsNullOrEmpty(finalDisplayUpdate.Content));
//              _output.WriteLine($"微工作流: 收到最终 DisplayUpdate 状态 '{finalDisplayUpdate.StreamingStatus}'。");
//
//
//             // 3. 验证在整个过程中 *没有* 收到针对 contextBlockId 的状态更新
//             _output.WriteLine($"微工作流: 验证没有收到 Block '{contextBlockId}' 的状态更新...");
//              // 等待一小段时间，确保没有延迟的消息进来
//              await Task.Delay(TimeSpan.FromSeconds(2));
//             bool receivedBlockStatusUpdate = await collector.DidNotReceiveMessageAsync<BlockStatusUpdateDto>(
//                 msg => msg.BlockId == contextBlockId, TimeSpan.Zero); // 用 Zero 超时检查已接收队列
//              Assert.True(receivedBlockStatusUpdate, $"不应收到 Block '{contextBlockId}' 的状态更新。");
//               _output.WriteLine($"微工作流: 确认没有收到 Block '{contextBlockId}' 的状态更新。");
//
//
//             bool receivedStateUpdateSignal = await collector.DidNotReceiveMessageAsync<StateUpdateSignalDto>(
//                 msg => msg.BlockId == contextBlockId, TimeSpan.Zero);
//              Assert.True(receivedStateUpdateSignal, $"不应收到 Block '{contextBlockId}' 的状态信号。");
//              _output.WriteLine($"微工作流: 确认没有收到 Block '{contextBlockId}' 的状态信号。");
//
//
//             // 4. (可选) 再次通过 API 获取 Block 状态，确认未改变
//              _output.WriteLine($"微工作流: 再次通过 API 验证 Block '{contextBlockId}' 状态...");
//             var finalBlockResponse = await HttpClient.GetAsync($"/api/blocks/{contextBlockId}");
//             Assert.Equal(HttpStatusCode.OK, finalBlockResponse.StatusCode);
//             var finalBlockDetail = await finalBlockResponse.Content.ReadFromJsonAsync<BlockDetailDto>();
//             Assert.NotNull(finalBlockDetail);
//             Assert.Equal(initialBlockDetail.StatusCode, finalBlockDetail.StatusCode); // 状态应与触发前一致
//              _output.WriteLine($"微工作流: 确认 Block '{contextBlockId}' 状态 ({finalBlockDetail.StatusCode}) 未改变。");
//
//         }
//         finally
//         {
//             await StopHubAsync(connection);
//         }
//     }
}
// --- END OF FILE MicroWorkflowIntegrationTests.cs ---