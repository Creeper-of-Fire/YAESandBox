using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;
using static YAESandBox.PlayerServices.Save.Utils.TokenUtil;

namespace YAESandBox.PlayerServices.Save.SaveData;

/// <summary>
/// 提供通用的、基于Token的用户数据存储API。
/// 这是一个低层级API，负责在Token指定的位置进行JSON资源的读写操作。
/// </summary>
[ApiController]
[Route("api/v1/user-data/user-saves")]
[ApiExplorerSettings(GroupName = PlayerSaveModule.ProjectSaveSlotGroupName)]
public class UserSaveDataController(UserSaveDataService userSaveDataService) : AuthenticatedApiControllerBase
{
    private UserSaveDataService UserSaveDataService { get; } = userSaveDataService;


    // --- Create / Update ---

    /// <summary>
    /// 在Token指定的位置创建或更新一个JSON资源。
    /// </summary>
    /// <param name="filename">要操作的文件名</param>
    /// <param name="token">访问令牌，用于唯一地定位一个资源容器的位置。</param>
    /// <param name="jsonObject">要存储的 JSON 对象。</param>
    /// <response code="204">数据已成功保存。</response>
    /// <response code="400">请求无效，例如：Token无效或 JSON 格式错误。</response>
    [HttpPut("{*filename}")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveUserData(string filename, [Required] [FromQuery] string token,
        [Required] [FromBody] object jsonObject) =>
        await this.UserSaveDataService.SaveDataAsync(this.UserId, ParseToken(token), filename, jsonObject).ToActionResultAsync();

    // --- Read ---

    /// <summary>
    /// 读取Token指定位置的JSON资源。
    /// </summary>
    /// <param name="filename">要操作的文件名</param>
    /// <param name="token">访问令牌，用于唯一地定位一个资源容器的位置。</param>
    /// <returns>资源的 JSON 内容。</returns>
    /// <response code="200">成功返回资源内容。</response>
    /// <response code="404">未找到指定的资源。</response>
    [HttpGet("{*filename}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GetUserData(string filename, [Required] [FromQuery] string token) =>
        await this.UserSaveDataService.ReadDataAsync(this.UserId, ParseToken(token), filename).ToActionResultAsync();

    // --- Delete ---

    /// <summary>
    /// 删除Token指定位置的JSON资源。
    /// </summary>
    /// <param name="filename">要操作的文件名</param>
    /// <param name="token">访问令牌，用于唯一地定位一个资源容器的位置。</param>
    /// <response code="204">资源已成功删除。</response>
    /// <response code="404">未找到要删除的资源。</response>
    [HttpDelete("{*filename}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUserData(string filename, [Required] [FromQuery] string token) =>
        await this.UserSaveDataService.DeleteDataAsync(this.UserId, ParseToken(token), filename).ToActionResultAsync();


    // --- List ---

    /// <summary>
    /// 列出在Token指定位置下的所有资源名称。
    /// </summary>
    /// <param name="token">
    /// 访问令牌，代表要列出内容的容器位置。
    /// </param>
    /// <returns>文件名列表。</returns>
    /// <response code="200">成功返回资源名称列表（可能为空）。</response>
    /// <response code="400">Token包含无效字符。</response>
    [HttpGet("list")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<string>>> ListUserData([Required] [FromQuery] string token) =>
        await this.UserSaveDataService.ListDataAsync(this.UserId, ParseToken(token)).ToActionResultAsync();
}