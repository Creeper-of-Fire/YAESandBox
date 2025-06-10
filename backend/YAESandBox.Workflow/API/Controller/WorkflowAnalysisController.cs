using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Workflow.Analysis;
using YAESandBox.Workflow.Config;

namespace YAESandBox.Workflow.API.Controller;

[ApiController]
[Route("api/v1/workflows-configs/analysis")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class WorkflowAnalysisController(WorkflowValidationService validationService) : ControllerBase
{
    private WorkflowValidationService ValidationService { get; } = validationService;

    // --- DTOs for this controller ---

    /// <summary>
    /// 模块的分析结果
    /// </summary>
    public record ModuleAnalysisResult
    {
        /// <summary>
        /// 模块消费的输入参数
        /// </summary>
        [Required]
        public List<string> ConsumedVariables { get; init; } = [];

        /// <summary>
        /// 模块生产的输出参数
        /// </summary>
        [Required]
        public List<string> ProducedVariables { get; init; } = [];
    }

    /// <summary>
    /// 分析单个模块配置，动态计算其输入和输出变量。
    /// </summary>
    /// <remarks>
    /// 用于编辑器在用户修改模块配置时，实时获取其数据依赖，以增强智能提示和即时反馈。
    /// </remarks>
    /// <param name="moduleConfig">包含模块配置草稿的请求体。</param>
    /// <returns>该模块消费和生产的变量列表。</returns>
    [HttpPost("analyze-module")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ModuleAnalysisResult), StatusCodes.Status200OK)]
    public ActionResult<ModuleAnalysisResult> AnalyzeModule([FromBody] AbstractModuleConfig moduleConfig)
    {
        var analysisResult = new ModuleAnalysisResult
        {
            ConsumedVariables = moduleConfig.GetConsumedVariables(),
            ProducedVariables = moduleConfig.GetProducedVariables()
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
    public ActionResult<WorkflowValidationReport> ValidateWorkflow([FromBody] WorkflowProcessorConfig workflowConfig)
    {
        return this.Ok(this.ValidationService.Validate(workflowConfig));
    }
}