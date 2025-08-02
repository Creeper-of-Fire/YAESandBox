using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Schema;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.API.Controller;

/// <summary>
/// 描述一个需要动态加载的前端组件资源。
/// </summary>
public record DynamicComponentAsset
{
    /// <summary>
    /// 插件的唯一名称，用于构建URL和识别。
    /// </summary>
    [Required]
    public required string PluginName { get; init; }

    /// <summary>
    /// 组件类型："Vue" 或 "WebComponent"。
    /// </summary>
    [Required]
    public required string ComponentType { get; init; }

    /// <summary>
    /// 组件脚本的完整可访问URL。
    /// </summary>
    [Required]
    public required string ScriptUrl { get; init; }

    /// <summary>
    /// 组件样式表的完整可访问URL（如果存在）。
    /// </summary>
    [Required]
    public string? StyleUrl { get; init; }
}

/// <summary>
/// 获取符文 Schemas 的 API 的完整响应体。
/// </summary>
public record RuneSchemasResponse
{
    /// <summary>
    /// 符文 Schema 的字典，键是符文类型名。
    /// </summary>
    [Required]
    public required Dictionary<string, object> Schemas { get; init; }

    /// <summary>
    /// 所有在 Schemas 中引用到的、需要动态加载的前端组件资源列表。
    /// </summary>
    [Required]
    public required List<DynamicComponentAsset> DynamicAssets { get; init; }
}

/// <summary>
/// 提供工作流相关配置（工作流、步骤、符文）的全局管理和Schema信息。
/// </summary>
/// <param name="workflowConfigFileService">工作流配置文件服务。</param>
/// <param name="webHostEnvironment">Web 主机环境。</param>
[ApiController]
[Route("api/v1/workflows-configs/global-runes")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class RuneConfigController(WorkflowConfigFileService workflowConfigFileService, IWebHostEnvironment webHostEnvironment)
    : AuthenticatedApiControllerBase
{
    private WorkflowConfigFileService WorkflowConfigFileService { get; } = workflowConfigFileService;
    private IWebHostEnvironment WebHostEnvironment { get; } = webHostEnvironment;

    private List<DynamicComponentAsset> DiscoverDynamicAssets()
    {
        var assets = new List<DynamicComponentAsset>();
        string pluginsRootDirectory = Path.Combine(this.WebHostEnvironment.ContentRootPath, "Plugins");

        if (!Directory.Exists(pluginsRootDirectory))
        {
            return assets;
        }

        string[] pluginDirectories = Directory.GetDirectories(pluginsRootDirectory);
        foreach (string pluginDir in pluginDirectories)
        {
            string pluginWwwRoot = Path.Combine(pluginDir, "wwwroot");
            if (!Directory.Exists(pluginWwwRoot)) continue;

            string pluginName = new DirectoryInfo(pluginDir).Name;

            // 检查 Vue 组件包
            string vueScriptPath = Path.Combine(pluginWwwRoot, "vue-bundle.js");
            string vueStylePath = Path.Combine(pluginWwwRoot, "vue-bundle.css"); // 对应的CSS文件
            if (System.IO.File.Exists(vueScriptPath))
            {
                assets.Add(new DynamicComponentAsset
                {
                    PluginName = pluginName,
                    ComponentType = "Vue",
                    ScriptUrl = $"/plugins/{pluginName}/vue-bundle.js",
                    StyleUrl = System.IO.File.Exists(vueStylePath) ? $"/plugins/{pluginName}/vue-bundle.css" : null
                });
            }

            // 检查 WebComponent 组件包
            string wcScriptPath = Path.Combine(pluginWwwRoot, "web-bundle.js");
            string wcStylePath = Path.Combine(pluginWwwRoot, "web-bundle.css"); // 对应的CSS文件
            if (System.IO.File.Exists(wcScriptPath))
            {
                assets.Add(new DynamicComponentAsset
                {
                    PluginName = pluginName,
                    ComponentType = "WebComponent",
                    ScriptUrl = $"/plugins/{pluginName}/web-bundle.js",
                    StyleUrl = System.IO.File.Exists(wcStylePath) ? $"/plugins/{pluginName}/web-bundle.css" : null
                });
            }
        }

        return assets;
    }

    /// <summary>
    /// 获取所有注册的符文配置类型的表单 Schema 结构 (JSON Schema 格式，包含 UI 指令)，并附带它们依赖的动态前端组件资源。
    /// 用于前端动态生成这些类型配置的【新建】或【编辑】表单骨架。
    /// </summary>
    /// <returns>一个包含所有 Schemas 和所需脚本内容的对象。</returns>
    [HttpGet("all-rune-configs-schemas")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(RuneSchemasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<RuneSchemasResponse>> GetAllRuneSchemas()
    {
        var schemas = new Dictionary<string, object>();
        var allRuneTypes = RuneConfigTypeResolver.GetAllRuneConfigTypes();

        foreach (var type in allRuneTypes)
        {
            try
            {
                string schemaJson = VueFormSchemaGenerator.GenerateSchemaJson(type, settings =>
                {
                    settings.SchemaProcessors.Add(new RuneRuleAttributeProcessor());
                    settings.SchemaProcessors.Add(new VueComponentRendererSchemaProcessor());
                    settings.SchemaProcessors.Add(new WebComponentRendererSchemaProcessor());
                    settings.SchemaProcessors.Add(new MonacoEditorRendererSchemaProcessor());
                });

                var value = JsonNode.Parse(schemaJson);
                if (value != null)
                {
                    schemas[type.Name] = value;
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult<ActionResult<RuneSchemasResponse>>(this.StatusCode(StatusCodes.Status500InternalServerError,
                    $"为类型 '{type.Name}' 生成 Schema 时发生错误: {ex.Message}"));
            }
        }

        var dynamicAssets = this.DiscoverDynamicAssets();

        var response = new RuneSchemasResponse
        {
            Schemas = schemas,
            DynamicAssets = dynamicAssets
        };

        return Task.FromResult<ActionResult<RuneSchemasResponse>>(this.Ok(response));
    }


    /// <summary>
    /// 获取所有全局符文配置的列表。
    /// </summary>
    /// <returns>包含所有全局符文配置的列表。</returns>
    /// <response code="200">成功获取所有全局符文配置的列表。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Dictionary<string, JsonResultDto<AbstractRuneConfig>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Dictionary<string, JsonResultDto<AbstractRuneConfig>>>> GetAllGlobalRuneConfigs() =>
        await this.WorkflowConfigFileService.FindAllRuneConfig(this.UserId).ToActionResultAsync(dic =>
            dic.ToDictionary(kv => kv.Key, kv => JsonResultDto<AbstractRuneConfig>.ToJsonResultDto(kv.Value)));

    /// <summary>
    /// 获取指定 ID 的全局符文配置。
    /// </summary>
    /// <param name="runeId">符文配置的唯一 ID。</param>
    /// <returns>指定 ID 的符文配置。</returns>
    /// <response code="200">成功获取指定的符文配置。</response>
    /// <response code="404">未找到指定 ID 的符文配置。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet("{runeId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AbstractRuneConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AbstractRuneConfig>> GetGlobalRuneConfigById(string runeId) =>
        await this.WorkflowConfigFileService.FindRuneConfig(this.UserId, runeId).ToActionResultAsync();

    /// <summary>
    /// 创建或更新全局符文配置 (Upsert)。
    /// 如果指定 ID 的符文配置已存在，则更新它；如果不存在，则创建它。
    /// 前端负责生成并提供符文的唯一 ID (GUID)。
    /// </summary>
    /// <param name="runeId">要创建或更新的符文配置的唯一 ID。</param>
    /// <param name="abstractRuneConfig">符文配置数据。</param>
    /// <returns>操作成功的响应。</returns>
    /// <response code="204">符文配置已成功更新/创建。</response>
    /// <response code="400">请求无效，例如：请求体为空或格式错误。</response>
    /// <response code="500">保存配置时发生内部服务器错误。</response>
    [HttpPut("{runeId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpsertGlobalRuneConfig(string runeId, [FromBody] AbstractRuneConfig abstractRuneConfig) =>
        await this.WorkflowConfigFileService.SaveRuneConfig(this.UserId, runeId, abstractRuneConfig).ToActionResultAsync();

    /// <summary>
    /// 删除指定 ID 的全局符文配置。
    /// </summary>
    /// <param name="runeId">要删除的符文配置的唯一 ID。</param>
    /// <returns>删除成功的响应。</returns>
    /// <response code="204">符文配置已成功删除。</response>
    /// <response code="500">删除配置时发生内部服务器错误。</response>
    [HttpDelete("{runeId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteGlobalRuneConfig(string runeId) =>
        await this.WorkflowConfigFileService.DeleteRuneConfig(this.UserId, runeId).ToActionResultAsync();
}