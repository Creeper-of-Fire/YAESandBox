using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.WorkflowService.Analysis;

namespace YAESandBox.Workflow.API.Controller;

/// <summary>
/// 分析符文的配置文件
/// </summary>
/// <param name="workflowValidationService"></param>
/// <param name="tuumAnalysisService"></param>
/// <param name="runeAnalysisService"></param>
[ApiController]
[Route("api/v1/workflows-configs/analysis")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class WorkflowAnalysisController(
    WorkflowValidationService workflowValidationService,
    TuumAnalysisService tuumAnalysisService,
    RuneAnalysisService runeAnalysisService
) : AuthenticatedApiControllerBase
{
    private WorkflowValidationService WorkflowValidationService { get; } = workflowValidationService;
    private TuumAnalysisService TuumAnalysisService { get; } = tuumAnalysisService;
    private RuneAnalysisService RuneAnalysisService { get; } = runeAnalysisService;

    // --- DTOs for this controller ---
    /// <summary>
    /// 为 analyze-rune 端点设计的请求体。
    /// </summary>
    public record RuneAnalysisRequest
    {
        /// <summary>
        /// 需要被分析和校验的目标符文配置。
        /// </summary>
        [Required]
        public required AbstractRuneConfig RuneToAnalyze { get; init; }
    }

    /// <summary>
    /// 对单个符文配置进行全面的分析和校验。
    /// </summary>
    /// <remarks>
    /// 用于符文编辑器在用户修改配置时，实时获取其数据依赖和校验状态。
    /// </remarks>
    /// <param name="request">包含符文配置及其可选上下文的请求体。</param>
    /// <returns>该符文的输入、输出和所有校验消息。</returns>
    [HttpPost("analyze-rune")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(RuneAnalysisResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<RuneAnalysisResult>> AnalyzeRune([FromBody] RuneAnalysisRequest request)
    {
        string userId = this.UserId;
        return await this.RuneAnalysisService.AnalyzeAsync(request.RuneToAnalyze, userId);
    }

    /// <summary>
    /// 对单个枢机（Tuum）配置草稿进行高级分析（不包含符文级校验）。
    /// </summary>
    /// <remarks>
    /// 用于枢机编辑器在用户进行连接、映射等操作时，分析枢机的接口、内部变量和数据流。
    /// 具体的符文级校验由符文校验端点负责。
    /// </remarks>
    /// <param name="tuumConfig">枢机配置的完整草稿。</param>
    /// <returns>一份包含枢机接口和数据流分析的报告。</returns>
    [HttpPost("analyze-tuum")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TuumAnalysisResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<TuumAnalysisResult>> AnalyzeTuum([FromBody] TuumConfig tuumConfig)
    {
        string userId = this.UserId;

        // --- 步骤 1: 预处理，填充 TuumConfig 中所有符文的缓存 ---
        // 我们对所有符文调用分析，而不仅仅是引用符文，以简化逻辑
        // AnalyzeAsync 内部会处理不同类型的符文
        foreach (var rune in tuumConfig.Runes)
        {
            // 我们不需要关心 AnalyzeAsync 的返回结果，
            // 它的副作用（更新 ReferenceRuneConfig 的缓存）才是我们需要的。
            _ = await this.RuneAnalysisService.AnalyzeAsync(rune, userId);
        }

        // --- 步骤 2: 现在可以安全地同步调用 TuumAnalysisService ---
        // 因为所有 ReferenceRuneConfig 都已经被“填充”了真实数据。
        var analysisResult = this.TuumAnalysisService.Analyze(tuumConfig);

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
    // TODO 对应的校验还没有完成
    [HttpPost("validate-workflow")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WorkflowValidationReport), StatusCodes.Status200OK)]
    public ActionResult<WorkflowValidationReport> ValidateWorkflow([FromBody] WorkflowConfig workflowConfig) =>
        this.Ok(this.WorkflowValidationService.Validate(workflowConfig));
}