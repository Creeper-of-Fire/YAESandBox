using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication.API;
using YAESandBox.Depend.AspNetCore.Controller.ResultToAction;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Workflow.Core.Config;
using YAESandBox.Workflow.Core.Config.Stored;
using YAESandBox.Workflow.Core.Runtime.WorkflowService;

namespace YAESandBox.Workflow.API.API.Controller;

/// <summary>
/// 提供全局枢机配置 (Tuum) 的管理功能。
/// </summary>
/// <param name="workflowConfigFindService">工作流配置文件服务。</param>
[ApiController]
[Route("api/v1/workflows-configs/global-tuums")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class TuumConfigController(WorkflowConfigFindService workflowConfigFindService) : AuthenticatedApiControllerBase
{
    private WorkflowConfigFindService WorkflowConfigFindService { get; } = workflowConfigFindService;

    /// <summary>
    /// 获取所有全局枢机配置的列表。
    /// </summary>
    /// <returns>包含所有全局枢机配置的列表。</returns>
    /// <response code="200">成功获取所有全局枢机配置的列表。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Dictionary<string, JsonResultDto<StoredConfig<TuumConfig>>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Dictionary<string, JsonResultDto<StoredConfig<TuumConfig>>>>> GetAllGlobalTuumConfigs() =>
        await this.WorkflowConfigFindService.FindAllTuumConfig(this.UserId).ToActionResultAsync(dic =>
            dic.ToDictionary(kv => kv.Key, kv => JsonResultDto<StoredConfig<TuumConfig>>.ToJsonResultDto(kv.Value)));

    /// <summary>
    /// 获取指定 ID 的全局枢机配置。
    /// </summary>
    /// <param name="storeId">枢机配置的唯一 ID。</param>
    /// <returns>指定 ID 的枢机配置。</returns>
    /// <response code="200">成功获取指定的枢机配置。</response>
    /// <response code="404">未找到指定 ID 的枢机配置。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet("{storeId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StoredConfig<TuumConfig>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StoredConfig<TuumConfig>>> GetGlobalTuumConfigById(string storeId) =>
        await this.WorkflowConfigFindService.FindTuumConfig(this.UserId, storeId).ToActionResultAsync();

    /// <summary>
    /// 创建或更新全局枢机配置 (Upsert)。
    /// </summary>
    /// <param name="storeId">要创建或更新的枢机配置的唯一 ID。</param>
    /// <param name="tuumConfig">枢机配置数据。</param>
    /// <returns>操作成功的响应。</returns>
    /// <response code="204">枢机配置已成功更新/创建。</response>
    /// <response code="500">保存配置时发生内部服务器错误。</response>
    [HttpPut("{storeId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpsertGlobalTuumConfig(string storeId, [FromBody] StoredConfig<TuumConfig> tuumConfig) =>
        await this.WorkflowConfigFindService.SaveTuumConfig(this.UserId, storeId, tuumConfig).ToActionResultAsync();

    /// <summary>
    /// 删除指定 ID 的全局枢机配置。
    /// </summary>
    /// <param name="storeId">要删除的枢机配置的唯一 ID。</param>
    /// <returns>删除成功的响应。</returns>
    /// <response code="204">枢机配置已成功删除。</response>
    /// <response code="500">删除配置时发生内部服务器错误。</response>
    [HttpDelete("{storeId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteGlobalTuumConfig(string storeId) =>
        await this.WorkflowConfigFindService.DeleteTuumConfig(this.UserId, storeId).ToActionResultAsync();
}