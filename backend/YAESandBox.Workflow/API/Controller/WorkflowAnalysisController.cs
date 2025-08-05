using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.Core.Analysis;
using YAESandBox.Workflow.Rune;

namespace YAESandBox.Workflow.API.Controller;

/// <summary>
/// 分析符文的配置文件
/// </summary>
/// <param name="validationService"></param>
[ApiController]
[Route("api/v1/workflows-configs/analysis")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class WorkflowAnalysisController(WorkflowValidationService validationService) : AuthenticatedApiControllerBase
{
    private WorkflowValidationService ValidationService { get; } = validationService;

    // --- DTOs for this controller ---

    /// <summary>
    /// 符文的分析结果
    /// </summary>
    public record RuneAnalysisResult
    {
        /// <summary>
        /// 符文消费的输入参数
        /// </summary>
        [Required]
        public List<string> ConsumedVariables { get; init; } = [];

        /// <summary>
        /// 符文生产的输出参数
        /// </summary>
        [Required]
        public List<string> ProducedVariables { get; init; } = [];
    }

    /// <summary>
    /// 分析单个符文配置，动态计算其输入和输出变量。
    /// </summary>
    /// <remarks>
    /// 用于编辑器在用户修改符文配置时，实时获取其数据依赖，以增强智能提示和即时反馈。
    /// </remarks>
    /// <param name="runeConfig">包含符文配置草稿的请求体。</param>
    /// <returns>该符文消费和生产的变量列表。</returns>
    [HttpPost("analyze-rune")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(RuneAnalysisResult), StatusCodes.Status200OK)]
    public ActionResult<RuneAnalysisResult> AnalyzeRune([FromBody] AbstractRuneConfig runeConfig)
    {
        var analysisResult = new RuneAnalysisResult
        {
            ConsumedVariables = runeConfig.GetConsumedSpec(),
            ProducedVariables = runeConfig.GetProducedSpec()
        };

        return this.Ok(analysisResult);
    }

    /// <summary>
    /// 对整个工作流配置草稿进行全面的静态校验。
    /// </summary>
    /// <remarks>
    /// 用于编辑器在用户编辑时，通过防抖调用此API，获取一份完整的“健康报告”，并在UI上高亮所有潜在的逻辑错误和警告。
    /// </remarks>
    /// <param name="workflowConfig">工作流配置的完整草稿。</param>
    /// <returns>一份包含所有校验信息的结构化报告。</returns>
    [HttpPost("validate-workflow")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WorkflowValidationReport), StatusCodes.Status200OK)]
    public ActionResult<WorkflowValidationReport> ValidateWorkflow([FromBody] WorkflowConfig workflowConfig)
    {
        return this.Ok(this.ValidationService.Validate(workflowConfig));
    }
}