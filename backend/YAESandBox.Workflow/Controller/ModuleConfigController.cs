using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.Schema;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.Controller;

/// <summary>
/// 提供工作流相关配置（工作流、步骤、模块）的全局管理和Schema信息。
/// </summary>
/// <param name="workflowConfigFileService">工作流配置文件服务。</param>
[ApiController]
[Route("api/v1/workflows-configs/global-modules")]
public class ModuleConfigController(WorkflowConfigFileService workflowConfigFileService) : ControllerBase
{
    private WorkflowConfigFileService WorkflowConfigFileService { get; } = workflowConfigFileService;

    /// <summary>
    /// 获取所有注册的模块配置类型的表单 Schema 结构 (JSON Schema 格式，包含 UI 指令)。
    /// 用于前端动态生成这些类型配置的【新建】或【编辑】表单骨架。
    /// </summary>
    /// <returns>一个字典，键是模块类型名称 (如 "PromptGenerationModuleConfig")，值是其对应的 JSON Schema 对象。</returns>
    [HttpGet("all-module-configs-schemas")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Dictionary<string, JsonNode>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<Dictionary<string, JsonNode>> GetAllModuleSchemas()
    {
        var allModuleTypeMappings = ModuleConfigTypeResolver.GetModuleTypeMappings();
        var schemas = new Dictionary<string, JsonNode>();

        foreach (var kvp in allModuleTypeMappings)
        {
            try
            {
                string schemaJson = VueFormSchemaGenerator.GenerateSchemaJson(kvp.Value);
                var value = JsonNode.Parse(schemaJson);
                if (value == null) continue;
                schemas[kvp.Key] = value;
            }
            catch (JsonException jsonEx)
            {
                // 如果任何一个 Schema 生成失败，我们认为这是一个严重的后端问题，返回 500
                return this.StatusCode(StatusCodes.Status500InternalServerError,
                    $"生成类型 '{kvp.Key}' 的 Schema 时发生 JSON 格式错误: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                return this.StatusCode(StatusCodes.Status500InternalServerError, $"为类型 '{kvp.Key}' 生成 Schema 时发生内部错误: {ex.Message}");
            }
        }

        return this.Ok(schemas);
    }


    /// <summary>
    /// 获取所有全局模块配置的列表。
    /// </summary>
    /// <returns>包含所有全局模块配置的列表。</returns>
    /// <response code="200">成功获取所有全局模块配置的列表。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IEnumerable<IModuleConfig>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<IModuleConfig>>> GetAllGlobalModuleConfigs() =>
        await this.WorkflowConfigFileService.FindAllModuleConfig().ToActionResultAsync();

    /// <summary>
    /// 获取指定 ID 的全局模块配置。
    /// </summary>
    /// <param name="moduleId">模块配置的唯一 ID。</param>
    /// <returns>指定 ID 的模块配置。</returns>
    /// <response code="200">成功获取指定的模块配置。</response>
    /// <response code="404">未找到指定 ID 的模块配置。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet("{moduleId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IModuleConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IModuleConfig>> GetGlobalModuleConfigById(string moduleId) =>
        await this.WorkflowConfigFileService.FindModuleConfig(moduleId).ToActionResultAsync();

    /// <summary>
    /// 创建或更新全局模块配置 (Upsert)。
    /// 如果指定 ID 的模块配置已存在，则更新它；如果不存在，则创建它。
    /// 前端负责生成并提供模块的唯一 ID (GUID)。
    /// </summary>
    /// <param name="moduleId">要创建或更新的模块配置的唯一 ID。</param>
    /// <param name="moduleConfig">模块配置数据。</param>
    /// <returns>操作成功的响应。</returns>
    /// <response code="204">模块配置已成功更新/创建。</response>
    /// <response code="400">请求无效，例如：请求体为空或格式错误。</response>
    /// <response code="500">保存配置时发生内部服务器错误。</response>
    [HttpPut("{moduleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpsertGlobalModuleConfig(string moduleId, [FromBody] IModuleConfig moduleConfig) =>
        await this.WorkflowConfigFileService.SaveModuleConfig(moduleId, moduleConfig).ToActionResultAsync();

    /// <summary>
    /// 删除指定 ID 的全局模块配置。
    /// </summary>
    /// <param name="moduleId">要删除的模块配置的唯一 ID。</param>
    /// <returns>删除成功的响应。</returns>
    /// <response code="204">模块配置已成功删除。</response>
    /// <response code="500">删除配置时发生内部服务器错误。</response>
    [HttpDelete("{moduleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteGlobalModuleConfig(string moduleId) =>
        await this.WorkflowConfigFileService.DeleteModuleConfig(moduleId).ToActionResultAsync();
}