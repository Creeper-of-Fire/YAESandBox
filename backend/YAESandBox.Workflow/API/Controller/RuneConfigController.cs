using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Schema;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.API.Controller;

// TODO: [PluginManagement] 考虑在多插件场景下，处理前端组件命名冲突问题。
//  - 方案1: 强制约定插件组件名称需以插件名作为前缀。
//  - 方案2: 后端在DiscoverDynamicAssets时，扫描所有插件声明的组件名，
//           若有冲突，则抛出错误或自动重命名Schema中的x-vue-component/x-web-component指令值。
//  - 方案3: 前端插件加载器对不同插件的同名组件进行命名空间隔离。
//  目前，假定所有插件组件名称在全局范围内是唯一的。

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
/// 提供工作流相关配置（工作流、祝祷、符文）的全局管理和Schema信息。
/// </summary>
/// <param name="workflowConfigFileService">工作流配置文件服务。</param>
/// <param name="pluginAssetService">插件资源发现服务。</param>
/// <param name="pluginDiscoveryService">插件总发现服务。</param>
[ApiController]
[Route("api/v1/workflows-configs/global-runes")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class RuneConfigController(
    WorkflowConfigFileService workflowConfigFileService,
    IPluginAssetService pluginAssetService,
    IPluginDiscoveryService pluginDiscoveryService)
    : AuthenticatedApiControllerBase
{
    private WorkflowConfigFileService WorkflowConfigFileService { get; } = workflowConfigFileService;
    private IPluginAssetService PluginAssetService { get; } = pluginAssetService;
    private IPluginDiscoveryService PluginDiscoveryService { get; } = pluginDiscoveryService;

    private List<DynamicComponentAsset> DiscoverDynamicAssets()
    {
        var assets = new List<DynamicComponentAsset>();
        var allPlugins = this.PluginAssetService.GetAllPlugins();

        foreach (var plugin in allPlugins)
        {
            // 现在，我们遵循的是工作流模块内部的约定
            // "vue-bundle.js" 和 "web-bundle.js" 是工作流系统与插件生态间的一种契约。
            // 这种约定是允许的，因为它属于模块自身领域的知识。

            // 检查 Vue 组件包 (约定)
            if (plugin.HasAsset("vue-bundle.js"))
            {
                assets.Add(new DynamicComponentAsset
                {
                    PluginName = plugin.Name,
                    ComponentType = "Vue",
                    ScriptUrl = plugin.GetAssetUrl("vue-bundle.js"),
                    StyleUrl = plugin.HasAsset("vue-bundle.css")
                        ? plugin.GetAssetUrl("vue-bundle.css")
                        : null
                });
            }

            // 检查 WebComponent 组件包 (约定)
            if (plugin.HasAsset("web-bundle.js"))
            {
                assets.Add(new DynamicComponentAsset
                {
                    PluginName = plugin.Name,
                    ComponentType = "WebComponent",
                    ScriptUrl = plugin.GetAssetUrl("web-bundle.js"),
                    StyleUrl = plugin.HasAsset("web-bundle.css")
                        ? plugin.GetAssetUrl("web-bundle.css")
                        : null
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
                    settings.SchemaProcessors.Add(new MonacoEditorRendererSchemaProcessor(this.PluginDiscoveryService));
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