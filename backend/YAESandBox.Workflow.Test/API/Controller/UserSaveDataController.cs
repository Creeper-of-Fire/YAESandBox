using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;

namespace YAESandBox.Workflow.Test.API.Controller;

/// <summary>
/// 用于测试的用户自定义存档数据存储 API。
/// </summary>
[ApiController]
[Route("api/v1/test/user-saves")]
[ApiExplorerSettings(GroupName = WorkflowTestModule.WorkflowTestGroupName)]
public class UserSaveDataController(UserSaveDataService userSaveDataService) : AuthenticatedApiControllerBase
{
    private UserSaveDataService UserSaveDataService { get; } = userSaveDataService;

    private static string[] ParsePath(string? path) =>
        path?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

    // --- Create / Update ---

    /// <summary>
    /// 创建或更新用户存档目录中的一个 JSON 文件。
    /// </summary>
    /// <param name="path">资源的相对路径，使用斜杠 (/) 分隔。例如: 'my-folder/my-file'。</param>
    /// <param name="jsonData">要存储的 JSON 字符串。</param>
    /// <response code="204">数据已成功保存。</response>
    /// <response code="400">请求无效，例如：路径无效或 JSON 格式错误。</response>
    [HttpPut("{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveUserData(string path, [FromBody] string jsonData) =>
        await this.UserSaveDataService.SaveDataAsync(this.UserId, ParsePath(path), jsonData).ToActionResultAsync();

    // --- Read ---

    /// <summary>
    /// 读取用户存档目录中的一个 JSON 文件。
    /// </summary>
    /// <param name="path">资源的相对路径，例如: 'my-folder/my-file.json'。</param>
    /// <returns>文件的 JSON 内容。</returns>
    /// <response code="200">成功返回文件内容。</response>
    /// <response code="404">未找到指定的文件。</response>
    [HttpGet("{*path}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<string>> GetUserData(string path) =>
        await this.UserSaveDataService.ReadDataAsync(this.UserId, ParsePath(path)).ToActionResultAsync();

    // --- Delete ---

    /// <summary>
    /// 删除用户存档目录中的一个 JSON 文件。
    /// </summary>
    /// <param name="path">要删除的资源的相对路径，例如: 'my-folder/my-file.json'。</param>
    /// <response code="204">文件已成功删除。</response>
    /// <response code="404">未找到要删除的文件。</response>
    [HttpDelete("{*path}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUserData(string path) =>
        await this.UserSaveDataService.DeleteDataAsync(this.UserId, ParsePath(path)).ToActionResultAsync();


    // --- List ---

    /// <summary>
    /// 列出用户存档目录下指定子目录中的所有文件名。
    /// </summary>
    /// <param name="path">要列出内容的目录路径。如果为空（通过访问 /list 端点），则列出存档根目录。</param>
    /// <returns>文件名列表。</returns>
    /// <response code="200">成功返回文件名列表（可能为空）。</response>
    /// <response code="400">路径包含无效字符。</response>
    [HttpGet("list")] // 路由 1: 匹配 /api/v1/test/user-saves/list
    [HttpGet("list/{*path}")] // 路由 2: 匹配 /api/v1/test/user-saves/list/任何/子路径
    [Produces("application/json")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<string>>> ListUserData(string? path = null) =>
        await this.UserSaveDataService.ListDataAsync(this.UserId, ParsePath(path)).ToActionResultAsync();
}