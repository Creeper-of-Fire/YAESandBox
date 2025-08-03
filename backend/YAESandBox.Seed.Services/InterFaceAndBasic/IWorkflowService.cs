// --- START OF FILE IWorkflowService.cs ---

using YAESandBox.Seed.DTOs.WebSocket;

namespace YAESandBox.Seed.Services.InterFaceAndBasic;

/// <summary>
/// 服务接口，用于处理工作流的触发和执行。
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// 处理来自客户端的主工作流触发请求。
    /// 这会创建一个新的 Block 并启动一个异步的工作流执行。
    /// </summary>
    /// <param name="request">工作流触发请求 DTO。</param>
    /// <returns>一个 Task 代表异步操作。</returns>
    Task HandleMainWorkflowTriggerAsync(TriggerMainWorkflowRequestDto request);

    /// <summary>
    /// 处理来自客户端的微工作流触发请求。
    /// 这*不会*创建一个新的 Block 并启动一个异步的工作流执行。
    /// </summary>
    /// <param name="request">工作流触发请求 DTO。</param>
    /// <returns>一个 Task 代表异步操作。</returns>
    Task HandleMicroWorkflowTriggerAsync(TriggerMicroWorkflowRequestDto request);


    /// <summary>
    /// 处理来自客户端的冲突解决请求。
    /// </summary>
    /// <param name="request">冲突解决请求 DTO。</param>
    /// <returns>一个 Task 代表异步操作。</returns>
    Task HandleConflictResolutionAsync(ResolveConflictRequestDto request);

    /// <summary>
    /// 处理重新生成 Block 的请求。
    /// </summary>
    /// <param name="request">重新生成请求 DTO。</param>
    /// <returns>一个 Task 代表异步操作。</returns>
    Task HandleRegenerateBlockAsync(RegenerateBlockRequestDto request);
}
// --- END OF FILE IWorkflowService.cs ---