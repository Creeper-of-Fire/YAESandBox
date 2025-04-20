using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.API.Services.InterFaceAndBasic;
using YAESandBox.Depend; // For mapping DTO to Core object

namespace YAESandBox.API.Controllers;

/// <summary>
/// 处理原子操作的 API 控制器。
/// </summary>
[ApiController]
[Route("api/atomic/{blockId}")] // /api/atomic/{blockId}
public class AtomicController(
    IBlockWritService writServices,
    IBlockReadService readServices,
    INotifierService notifierService)
    : APINotifyControllerBase(readServices, writServices, notifierService)
{
    /// <summary>
    /// 对指定的 Block 执行一批原子化操作。
    /// 根据 Block 的当前状态，操作可能被立即执行或暂存。
    /// </summary>
    /// <param name="blockId">要执行操作的目标 Block 的 ID。</param>
    /// <param name="request">包含原子操作列表的请求体。</param>
    /// <returns>指示操作执行结果的 HTTP 状态码。</returns>
    /// <response code="200">操作已成功执行，若为Loading状态则还额外暂存了一份。</response>
    /// <response code="400">请求中包含无效的原子操作定义。</response>
    /// <response code="404">未找到具有指定 ID 的 Block。</response>
    /// <response code="409">Block 当前处于冲突状态 (ResolvingConflict)，需要先解决冲突。</response>
    /// <response code="500">执行操作时发生内部服务器错误。</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExecuteAtomicOperations(string blockId, [FromBody] BatchAtomicRequestDto request)
    {
        // 1. 使用辅助方法将 DTO 映射为核心原子操作对象
        List<AtomicOperationRequestDto> coreOperations;
        try
        {
            // 调用 DTO 辅助类中的扩展方法
            coreOperations = request.Operations;
        }
        // 捕获由 ToAtomicOperations (及其调用的 ToAtomicOperation 和 AtomicOperation 工厂方法)
        // 抛出的验证或解析异常 (例如无效的操作类型、操作符、空ID等)
        catch (ArgumentException ex)
        {
            Log.Warning($"原子操作映射失败: {ex.Message}");
            // 返回 400 Bad Request，指示请求数据有问题
            return this.BadRequest($"提供的原子操作无效: {ex.Message}");
        }
        // 捕获其他意外异常
        catch (Exception ex)
        {
            Log.Error(ex, $"映射原子操作时发生意外错误: {ex.Message}");
            return this.StatusCode(StatusCodes.Status500InternalServerError, "处理请求时发生内部错误。");
        }

        // 2. 调用 BlockManager 处理操作
        var result = await this.blockWritService.EnqueueOrExecuteAtomicOperationsAsync(blockId, coreOperations);

        if (result.resultCode != BlockResultCode.NotFound)
            await this.notifierService.NotifyBlockUpdateAsync(blockId, BlockDataFields.WorldState);

        // 3. 根据结果返回相应的状态码
        return result switch
        {
            (BlockResultCode.Success, BlockStatusCode.Loading) => this.Ok("操作已成功执行。"),
            (BlockResultCode.Success, BlockStatusCode.Idle) => this.Ok("操作已成功执行并暂存"),
            (BlockResultCode.NotFound, _) => this.NotFound($"未找到 ID 为 '{blockId}' 的 Block。"),
            (BlockResultCode.Error, BlockStatusCode.ResolvingConflict) => this.Conflict($"Block '{blockId}' 处于冲突状态。请先解决冲突。"),
            (BlockResultCode.Error, BlockStatusCode.Error) => this.StatusCode(StatusCodes.Status500InternalServerError, "执行期间发生错误。"),
            _ => this.StatusCode(StatusCodes.Status500InternalServerError, "发生意外的结果。")
        };
    }
}