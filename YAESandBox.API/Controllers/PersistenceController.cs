// --- START OF FILE PersistenceController.cs ---

using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using YAESandBox.Core.Block; // For BlockManager
using Microsoft.Net.Http.Headers;
using YAESandBox.Depend; // For ContentDispositionHeaderValue

namespace YAESandBox.API.Controllers;

[ApiController]
[Route("api/[controller]")] // /api/persistence
public class PersistenceController(IBlockManager blockManager) : ControllerBase
{
    // Inject BlockManager
    private IBlockManager blockManager { get; } = blockManager;


    /// <summary>
    /// 保存当前状态到文件。接收前端盲存数据，返回包含完整存档的 JSON 文件。
    /// </summary>
    [HttpPost("save")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SaveState([FromBody] object? blindStorageData) // Receive blind storage
    {
        try
        {
            // Use MemoryStream to avoid temporary files if archive is reasonably small
            var memoryStream = new MemoryStream();
            await this.blockManager.SaveToFileAsync(memoryStream, blindStorageData);
            memoryStream.Position = 0; // Reset stream position for reading

            // Return as file download
            return new FileStreamResult(memoryStream, "application/json")
            {
                FileDownloadName = $"yaesandbox_save_{DateTime.UtcNow:yyyyMMddHHmmss}.json"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save state.");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to save state.");
        }
    }

    /// <summary>
    /// 从上传的文件加载状态。替换当前内存中的所有状态。返回文件中的盲存数据。
    /// </summary>
    [HttpPost("load")]
    [Consumes("multipart/form-data")] // Expect file upload
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)] // Returns blind storage
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // No file or invalid file
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LoadState(IFormFile archiveFile)
    {
        if (archiveFile.Length == 0)
        {
            return BadRequest("No archive file uploaded.");
        }

        // Basic check for JSON extension (optional but good practice)
        var extension = Path.GetExtension(archiveFile.FileName);
        if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            // Allow anyway as per "don't consider too many errors"
            Log.Warning($"Loading file without .json extension: {archiveFile.FileName}");
            // return BadRequest("Invalid file type. Please upload a .json file.");
        }

        try
        {
            await using var stream = archiveFile.OpenReadStream();
            var blindStorage = await this.blockManager.LoadFromFileAsync(stream);
            // TODO: Notify clients about the state reset? (Maybe via INotifierService)
            // await _notifierService.NotifyGlobalStateResetAsync(); // Example notification
            return Ok(blindStorage); // Return the blind storage data
        }
        catch (JsonException jsonEx)
        {
            Log.Error(jsonEx, $"Failed to deserialize archive file: {archiveFile.FileName}");
            return BadRequest("Invalid archive file format."); // Specific error for bad JSON
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to load state from file: {archiveFile.FileName}");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to load state.");
        }
    }
}
// --- END OF FILE PersistenceController.cs ---