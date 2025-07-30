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
/// 提供全局步骤配置 (Step) 的管理功能。
/// </summary>
/// <param name="workflowConfigFileService">工作流配置文件服务。</param>
[ApiController]
[Route("api/v1/workflows-configs/global-steps")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class StepConfigController(WorkflowConfigFileService workflowConfigFileService) : AuthenticatedApiControllerBase
{
    private WorkflowConfigFileService WorkflowConfigFileService { get; } = workflowConfigFileService;

    /// <summary>
    /// 获取所有全局步骤配置的列表。
    /// </summary>
    /// <returns>包含所有全局步骤配置的列表。</returns>
    /// <response code="200">成功获取所有全局步骤配置的列表。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Dictionary<string, JsonResultDto<StepProcessorConfig>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Dictionary<string, JsonResultDto<StepProcessorConfig>>>> GetAllGlobalStepConfigs() =>
        await this.WorkflowConfigFileService.FindAllStepConfig(this.UserId).ToActionResultAsync(dic =>
            dic.ToDictionary(kv => kv.Key, kv => JsonResultDto<StepProcessorConfig>.ToJsonResultDto(kv.Value)));

    /// <summary>
    /// 获取指定 ID 的全局步骤配置。
    /// </summary>
    /// <param name="stepId">步骤配置的唯一 ID。</param>
    /// <returns>指定 ID 的步骤配置。</returns>
    /// <response code="200">成功获取指定的步骤配置。</response>
    /// <response code="404">未找到指定 ID 的步骤配置。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet("{stepId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(StepProcessorConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StepProcessorConfig>> GetGlobalStepConfigById(string stepId) =>
        await this.WorkflowConfigFileService.FindStepConfig(this.UserId, stepId).ToActionResultAsync();

    /// <summary>
    /// 创建或更新全局步骤配置 (Upsert)。
    /// </summary>
    /// <param name="stepId">要创建或更新的步骤配置的唯一 ID。</param>
    /// <param name="stepConfig">步骤配置数据。</param>
    /// <returns>操作成功的响应。</returns>
    /// <response code="204">步骤配置已成功更新/创建。</response>
    /// <response code="500">保存配置时发生内部服务器错误。</response>
    [HttpPut("{stepId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpsertGlobalStepConfig(string stepId, [FromBody] StepProcessorConfig stepConfig) =>
        await this.WorkflowConfigFileService.SaveStepConfig(this.UserId, stepId, stepConfig).ToActionResultAsync();

    /// <summary>
    /// 删除指定 ID 的全局步骤配置。
    /// </summary>
    /// <param name="stepId">要删除的步骤配置的唯一 ID。</param>
    /// <returns>删除成功的响应。</returns>
    /// <response code="204">步骤配置已成功删除。</response>
    /// <response code="500">删除配置时发生内部服务器错误。</response>
    [HttpDelete("{stepId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteGlobalStepConfig(string stepId) =>
        await this.WorkflowConfigFileService.DeleteStepConfig(this.UserId, stepId).ToActionResultAsync();
}