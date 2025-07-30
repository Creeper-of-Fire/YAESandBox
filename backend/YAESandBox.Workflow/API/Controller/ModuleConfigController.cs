using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.Results;
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
/// 获取模块 Schemas 的 API 的完整响应体。
/// </summary>
public record ModuleSchemasResponse
{
    /// <summary>
    /// 模块 Schema 的字典，键是模块类型名。
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
/// 提供工作流相关配置（工作流、步骤、模块）的全局管理和Schema信息。
/// </summary>
/// <param name="workflowConfigFileService">工作流配置文件服务。</param>
/// <param name="webHostEnvironment">Web 主机环境。</param>
[ApiController]
[Route("api/v1/workflows-configs/global-modules")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class ModuleConfigController(WorkflowConfigFileService workflowConfigFileService, IWebHostEnvironment webHostEnvironment)
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
    /// 获取所有注册的模块配置类型的表单 Schema 结构 (JSON Schema 格式，包含 UI 指令)，并附带它们依赖的动态前端组件资源。
    /// 用于前端动态生成这些类型配置的【新建】或【编辑】表单骨架。
    /// </summary>
    /// <returns>一个包含所有 Schemas 和所需脚本内容的对象。</returns>
    [HttpGet("all-module-configs-schemas")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ModuleSchemasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<ModuleSchemasResponse>> GetAllModuleSchemas()
    {
        var schemas = new Dictionary<string, object>();
        var allModuleTypes = ModuleConfigTypeResolver.GetAllModuleConfigTypes();

        foreach (var type in allModuleTypes)
        {
            try
            {
                string schemaJson = VueFormSchemaGenerator.GenerateSchemaJson(type, settings =>
                {
                    settings.SchemaProcessors.Add(new ModuleRuleAttributeProcessor());
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
                return Task.FromResult<ActionResult<ModuleSchemasResponse>>(this.StatusCode(StatusCodes.Status500InternalServerError,
                    $"为类型 '{type.Name}' 生成 Schema 时发生错误: {ex.Message}"));
            }
        }

        var dynamicAssets = this.DiscoverDynamicAssets();

        var response = new ModuleSchemasResponse
        {
            Schemas = schemas,
            DynamicAssets = dynamicAssets
        };

        return Task.FromResult<ActionResult<ModuleSchemasResponse>>(this.Ok(response));
    }


    /// <summary>
    /// 获取所有全局模块配置的列表。
    /// </summary>
    /// <returns>包含所有全局模块配置的列表。</returns>
    /// <response code="200">成功获取所有全局模块配置的列表。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Dictionary<string, JsonResultDto<AbstractModuleConfig>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Dictionary<string, JsonResultDto<AbstractModuleConfig>>>> GetAllGlobalModuleConfigs() =>
        await this.WorkflowConfigFileService.FindAllModuleConfig(this.UserId).ToActionResultAsync(dic =>
            dic.ToDictionary(kv => kv.Key, kv => JsonResultDto<AbstractModuleConfig>.ToJsonResultDto(kv.Value)));

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
    [ProducesResponseType(typeof(AbstractModuleConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AbstractModuleConfig>> GetGlobalModuleConfigById(string moduleId) =>
        await this.WorkflowConfigFileService.FindModuleConfig(this.UserId, moduleId).ToActionResultAsync();

    /// <summary>
    /// 创建或更新全局模块配置 (Upsert)。
    /// 如果指定 ID 的模块配置已存在，则更新它；如果不存在，则创建它。
    /// 前端负责生成并提供模块的唯一 ID (GUID)。
    /// </summary>
    /// <param name="moduleId">要创建或更新的模块配置的唯一 ID。</param>
    /// <param name="abstractModuleConfig">模块配置数据。</param>
    /// <returns>操作成功的响应。</returns>
    /// <response code="204">模块配置已成功更新/创建。</response>
    /// <response code="400">请求无效，例如：请求体为空或格式错误。</response>
    /// <response code="500">保存配置时发生内部服务器错误。</response>
    [HttpPut("{moduleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpsertGlobalModuleConfig(string moduleId, [FromBody] AbstractModuleConfig abstractModuleConfig) =>
        await this.WorkflowConfigFileService.SaveModuleConfig(this.UserId, moduleId, abstractModuleConfig).ToActionResultAsync();

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
        await this.WorkflowConfigFileService.DeleteModuleConfig(this.UserId, moduleId).ToActionResultAsync();
}