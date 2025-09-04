using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.PlayerServices.Save.Utils;

namespace YAESandBox.PlayerServices.Save.SaveSlot;

/// <summary>
/// 提供基于项目的用户存档槽管理 API。
/// </summary>
[ApiController]
[Route("api/v1/user-data/{projectUniqueName}/saves")]
[ApiExplorerSettings(GroupName = PlayerSaveModule.ProjectSaveSlotGroupName)]
public class ProjectSaveSlotController(ProjectSaveSlotService projectSaveSlotService) : AuthenticatedApiControllerBase
{
    private ProjectSaveSlotService ProjectSaveSlotService { get; } = projectSaveSlotService;

    /// <summary>
    /// 获取指定项目的所有存档槽列表。
    /// </summary>
    /// <param name="projectUniqueName">项目的唯一标识符。</param>
    /// <returns>存档槽列表。</returns>
    [HttpGet]
    [ProducesResponseType<IEnumerable<SaveSlot>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<SaveSlot>>> ListSaveSlots(string projectUniqueName) =>
        await this.ProjectSaveSlotService.ListSaveSlotsAsync(this.UserId, projectUniqueName).ToActionResultAsync();
    
    /// <summary>
    /// 获取指定项目的元数据/项目根级别数据的存储容器访问Token。
    /// </summary>
    /// <param name="projectUniqueName">项目的唯一标识符。</param>
    /// <returns>返回一个专用于项目根级别数据的token</returns>
    [HttpGet("meta")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<string>> GetMetaSlots(string projectUniqueName) =>
        Task.FromResult<ActionResult<string>>(TokenUtil.CreateToken(projectUniqueName));

    /// <summary>
    /// 为指定项目创建一个新的存档槽。
    /// </summary>
    /// <param name="projectUniqueName">项目的唯一标识符。</param>
    /// <param name="request">创建请求，包含名称和类型。</param>
    /// <returns>新创建的存档槽信息。</returns>
    [HttpPost]
    [ProducesResponseType<SaveSlot>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SaveSlot>> CreateSaveSlot(string projectUniqueName,
        [FromBody] CreateSaveSlotRequest request) =>
        await this.ProjectSaveSlotService.CreateSaveSlotAsync(this.UserId, projectUniqueName, request)
            .ToActionResultAsync(new ActionResultConversionOptions(StatusCodes.Status201Created));

    /// <summary>
    /// 复制一个现有的存档槽，以创建一个内容相同的新槽。
    /// </summary>
    /// <param name="projectUniqueName">项目的唯一标识符。</param>
    /// <param name="sourceSlotId">要复制的源存档槽的ID。</param>
    /// <param name="request">描述新存档槽元数据的请求体。</param>
    /// <returns>新创建的存档槽的完整信息。</returns>
    [HttpPost("{sourceSlotId}/copy")]
    [ProducesResponseType<SaveSlot>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaveSlot>> CopySaveSlot(string projectUniqueName, string sourceSlotId,
        [FromBody] CreateSaveSlotRequest request) =>
        await this.ProjectSaveSlotService.CopySaveSlotAsync(this.UserId, projectUniqueName, sourceSlotId, request)
            .ToActionResultAsync(new ActionResultConversionOptions(StatusCodes.Status201Created));

    /// <summary>
    /// 删除指定项目的一个存档槽。
    /// </summary>
    /// <param name="projectUniqueName">项目的唯一标识符。</param>
    /// <param name="slotId">要删除的存档槽ID。</param>
    /// <response code="204">存档槽已成功删除。</response>
    /// <response code="404">未找到指定的存档槽。</response>
    [HttpDelete("{slotId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSaveSlot(string projectUniqueName, string slotId) =>
        await this.ProjectSaveSlotService.DeleteSaveSlotAsync(this.UserId, projectUniqueName, slotId).ToActionResultAsync();
}