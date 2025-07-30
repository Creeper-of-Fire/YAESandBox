using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.API.Controller;

/// <summary>
/// 提供全局工作流配置 (Workflow) 的管理功能。
/// </summary>
/// <param name="workflowConfigFileService">工作流配置文件服务。</param>
[ApiController]
[Route("api/v1/workflows-configs/global-workflows")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class WorkflowConfigController(WorkflowConfigFileService workflowConfigFileService) : AuthenticatedApiControllerBase
{
    private WorkflowConfigFileService WorkflowConfigFileService { get; } = workflowConfigFileService;

    /// <summary>
    /// 获取所有全局工作流配置的列表。
    /// </summary>
    /// <returns>包含所有全局工作流配置的列表。</returns>
    /// <response code="200">成功获取所有全局工作流配置的列表。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Dictionary<string, JsonResultDto<WorkflowProcessorConfig>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Dictionary<string, JsonResultDto<WorkflowProcessorConfig>>>> GetAllGlobalWorkflowConfigs() =>
        await this.WorkflowConfigFileService.FindAllWorkflowConfig(this.UserId).ToActionResultAsync(dic =>
            dic.ToDictionary(kv => kv.Key, kv => JsonResultDto<WorkflowProcessorConfig>.ToJsonResultDto(kv.Value)));

    /// <summary>
    /// 获取指定 ID 的全局工作流配置。
    /// </summary>
    /// <param name="workflowId">工作流配置的唯一 ID。</param>
    /// <returns>指定 ID 的工作流配置。</returns>
    /// <response code="200">成功获取指定的工作流配置。</response>
    /// <response code="404">未找到指定 ID 的工作流配置。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet("{workflowId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WorkflowProcessorConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<WorkflowProcessorConfig>> GetGlobalWorkflowConfigById(string workflowId) =>
        await this.WorkflowConfigFileService.FindWorkflowConfig(this.UserId, workflowId).ToActionResultAsync();

    /// <summary>
    /// 创建或更新全局工作流配置 (Upsert)。
    /// </summary>
    /// <param name="workflowId">要创建或更新的工作流配置的唯一 ID。</param>
    /// <param name="workflowConfig">工作流配置数据。</param>
    /// <returns>操作成功的响应。</returns>
    /// <response code="204">工作流配置已成功更新/创建。</response>
    /// <response code="500">保存配置时发生内部服务器错误。</response>
    [HttpPut("{workflowId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpsertGlobalWorkflowConfig(string workflowId, [FromBody] WorkflowProcessorConfig workflowConfig) =>
        await this.WorkflowConfigFileService.SaveWorkflowConfig(this.UserId, workflowId, workflowConfig).ToActionResultAsync();

    /// <summary>
    /// 删除指定 ID 的全局工作流配置。
    /// </summary>
    /// <param name="workflowId">要删除的工作流配置的唯一 ID。</param>
    /// <returns>删除成功的响应。</returns>
    /// <response code="204">工作流配置已成功删除。</response>
    /// <response code="500">删除配置时发生内部服务器错误。</response>
    [HttpDelete("{workflowId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteGlobalWorkflowConfig(string workflowId) =>
        await this.WorkflowConfigFileService.DeleteWorkflowConfig(this.UserId, workflowId).ToActionResultAsync();
}