// --- START OF FILE PersistenceController.cs ---

using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using YAESandBox.Core.Block; // For BlockManager
using YAESandBox.Depend; // For Log

namespace YAESandBox.API.Controllers;

/// <summary>
/// 提供用于保存和加载整个 YAESandBox 状态的 API 端点。
/// </summary>
[ApiController]
[Route("api/[controller]")] // /api/persistence
public class PersistenceController(IBlockManager blockManager) : ControllerBase
{
    // 注入 BlockManager
    private IBlockManager blockManager { get; } = blockManager;


    /// <summary>
    /// 保存当前 YAESandBox 的完整状态（包括所有 Block、WorldState、GameState）到一个 JSON 文件。
    /// 客户端可以（可选地）在请求体中提供需要一同保存的“盲存”数据。
    /// </summary>
    /// <param name="blindStorageData">（可选）客户端提供的任意 JSON 格式的盲存数据，将原样保存在存档文件中。</param>
    /// <returns>一个包含完整存档内容的 JSON 文件流。</returns>
    /// <response code="200">成功生成并返回存档文件。</response>
    /// <response code="500">保存状态时发生内部服务器错误。</response>
    [HttpPost("save")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SaveState([FromBody] object? blindStorageData = null) // blindStorageData 是可选的
    {
        try
        {
            // 使用 MemoryStream 避免在存档文件较小时产生临时文件
            var memoryStream = new MemoryStream();
            // 调用 BlockManager 保存状态，并传入盲存数据
            await this.blockManager.SaveToFileAsync(memoryStream, blindStorageData);
            memoryStream.Position = 0; // 重置流位置以便读取

            // 返回文件下载结果
            return new FileStreamResult(memoryStream, "application/json")
            {
                // 生成包含时间戳的文件名
                FileDownloadName = $"yaesandbox_save_{DateTime.UtcNow:yyyyMMddHHmmss}.json"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存状态失败。");
            return this.StatusCode(StatusCodes.Status500InternalServerError, "保存状态失败。");
        }
    }

    /// <summary>
    /// 从上传的 JSON 存档文件加载 YAESandBox 的状态。
    /// 这将完全替换当前内存中的所有 Block、WorldState 和 GameState。
    /// 成功加载后，将返回存档文件中包含的“盲存”数据（如果存在）。
    /// </summary>
    /// <param name="archiveFile">包含 YAESandBox 存档的 JSON 文件。</param>
    /// <returns>存档文件中包含的盲存数据（如果存在）。</returns>
    /// <response code="200">成功加载状态，并返回盲存数据。</response>
    /// <response code="400">没有上传文件，或者上传的文件格式无效 (非 JSON 或内容损坏)。</response>
    /// <response code="500">加载状态时发生内部服务器错误。</response>
    [HttpPost("load")]
    [Consumes("multipart/form-data")] // 指定期望接收文件上传
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)] // 返回盲存数据
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // 没有文件或无效文件
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LoadState(IFormFile archiveFile)
    {
        if (archiveFile.Length == 0)
            return this.BadRequest("未上传存档文件。");

        // 对文件扩展名进行基本检查 (可选，但推荐)
        string extension = Path.GetExtension(archiveFile.FileName);
        if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            // 允许非 .json 文件，但记录警告
            Log.Warning($"正在加载非 .json 扩展名的文件: {archiveFile.FileName}");
        // return BadRequest("无效的文件类型。请上传一个 .json 文件。"); // 可以取消注释以强制执行
        try
        {
            // 使用 using 确保流被正确释放
            await using var stream = archiveFile.OpenReadStream();
            // 调用 BlockManager 加载状态，并获取盲存数据
            object? blindStorage = await this.blockManager.LoadFromFileAsync(stream);

            // TODO: (可选) 通知所有连接的客户端状态已重置？
            // 可以通过 INotifierService 实现一个全局通知方法
            // 例如：await _notifierService.NotifyGlobalStateResetAsync();

            // 返回从存档中恢复的盲存数据
            return this.Ok(blindStorage);
        }
        catch (JsonException jsonEx)
        {
            // 特别处理 JSON 解析错误
            Log.Error(jsonEx, $"反序列化存档文件失败: {archiveFile.FileName}");
            return this.BadRequest("无效的存档文件格式。"); // 返回 400 Bad Request
        }
        catch (Exception ex)
        {
            // 处理其他加载过程中的异常
            Log.Error(ex, $"从文件加载状态失败: {archiveFile.FileName}");
            return this.StatusCode(StatusCodes.Status500InternalServerError, "加载状态失败。");
        }
    }
}
// --- END OF FILE PersistenceController.cs ---