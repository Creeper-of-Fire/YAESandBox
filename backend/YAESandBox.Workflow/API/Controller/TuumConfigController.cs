using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.API.Controller;

/// <summary>
/// 提供全局祝祷配置 (Tuum) 的管理功能。
/// </summary>
/// <param name="workflowConfigFileService">工作流配置文件服务。</param>
[ApiController]
[Route("api/v1/workflows-configs/global-tuums")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class TuumConfigController(WorkflowConfigFileService workflowConfigFileService) : AuthenticatedApiControllerBase
{
    private WorkflowConfigFileService WorkflowConfigFileService { get; } = workflowConfigFileService;

    /// <summary>
    /// 获取所有全局祝祷配置的列表。
    /// </summary>
    /// <returns>包含所有全局祝祷配置的列表。</returns>
    /// <response code="200">成功获取所有全局祝祷配置的列表。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Dictionary<string, JsonResultDto<TuumConfig>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Dictionary<string, JsonResultDto<TuumConfig>>>> GetAllGlobalTuumConfigs() =>
        await this.WorkflowConfigFileService.FindAllTuumConfig(this.UserId).ToActionResultAsync(dic =>
            dic.ToDictionary(kv => kv.Key, kv => JsonResultDto<TuumConfig>.ToJsonResultDto(kv.Value)));

    /// <summary>
    /// 获取指定 ID 的全局祝祷配置。
    /// </summary>
    /// <param name="tuumId">祝祷配置的唯一 ID。</param>
    /// <returns>指定 ID 的祝祷配置。</returns>
    /// <response code="200">成功获取指定的祝祷配置。</response>
    /// <response code="404">未找到指定 ID 的祝祷配置。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet("{tuumId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TuumConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TuumConfig>> GetGlobalTuumConfigById(string tuumId) =>
        await this.WorkflowConfigFileService.FindTuumConfig(this.UserId, tuumId).ToActionResultAsync();

    /// <summary>
    /// 创建或更新全局祝祷配置 (Upsert)。
    /// </summary>
    /// <param name="tuumId">要创建或更新的祝祷配置的唯一 ID。</param>
    /// <param name="tuumConfig">祝祷配置数据。</param>
    /// <returns>操作成功的响应。</returns>
    /// <response code="204">祝祷配置已成功更新/创建。</response>
    /// <response code="500">保存配置时发生内部服务器错误。</response>
    [HttpPut("{tuumId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpsertGlobalTuumConfig(string tuumId, [FromBody] TuumConfig tuumConfig) =>
        await this.WorkflowConfigFileService.SaveTuumConfig(this.UserId, tuumId, tuumConfig).ToActionResultAsync();

    /// <summary>
    /// 删除指定 ID 的全局祝祷配置。
    /// </summary>
    /// <param name="tuumId">要删除的祝祷配置的唯一 ID。</param>
    /// <returns>删除成功的响应。</returns>
    /// <response code="204">祝祷配置已成功删除。</response>
    /// <response code="500">删除配置时发生内部服务器错误。</response>
    [HttpDelete("{tuumId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteGlobalTuumConfig(string tuumId) =>
        await this.WorkflowConfigFileService.DeleteTuumConfig(this.UserId, tuumId).ToActionResultAsync();
}