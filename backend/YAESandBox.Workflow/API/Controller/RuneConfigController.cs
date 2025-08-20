using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Schema;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.Rune;
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
    /// 插件的唯一ID，用于构建URL和识别。
    /// </summary>
    [Required]
    public required string PluginId { get; init; }

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
/// 提供工作流相关配置（工作流、枢机、符文）的全局管理和Schema信息。
/// </summary>
/// <param name="workflowConfigFileService">工作流配置文件服务。</param>
/// <param name="moduleProvider">加载的所有模块。</param>
[ApiController]
[Route("api/v1/workflows-configs/global-runes")]
[ApiExplorerSettings(GroupName = WorkflowConfigModule.WorkflowConfigGroupName)]
public class RuneConfigController(
    WorkflowConfigFileService workflowConfigFileService,
    IModuleProvider moduleProvider)
    : AuthenticatedApiControllerBase
{
    private WorkflowConfigFileService WorkflowConfigFileService { get; } = workflowConfigFileService;
    private IModuleProvider ModuleProvider { get; } = moduleProvider;

    private List<DynamicComponentAsset> DiscoverDynamicAssets()
    {
        var assets = new List<DynamicComponentAsset>();
        var allModules = this.ModuleProvider.AllModules;
        var allPlugins = allModules.OfType<IYaeSandBoxPlugin>();

        foreach (var plugin in allPlugins)
        {
            // 3. 直接通过插件的 Assembly 位置来推断 wwwroot 路径
            var assemblyLocation = plugin.GetType().Assembly.Location;
            if (string.IsNullOrEmpty(assemblyLocation)) continue;

            var pluginRootPath = Path.GetDirectoryName(assemblyLocation);
            if (pluginRootPath == null) continue;

            var wwwRootPath = Path.Combine(pluginRootPath, "wwwroot");
            if (!Directory.Exists(wwwRootPath)) continue;

            // 4. 定义检查文件和生成 URL 的本地函数，逻辑更清晰
            bool HasAsset(string fileName) => System.IO.File.Exists(Path.Combine(wwwRootPath, fileName));
            string GetAssetUrl(string fileName) => $"{plugin.ToRequestPath()}/{fileName}";

            // 检查 Vue 组件包 (约定)
            if (HasAsset("vue-bundle.js"))
            {
                assets.Add(new DynamicComponentAsset
                {
                    PluginId = plugin.Metadata.Id,
                    ComponentType = "Vue",
                    ScriptUrl = GetAssetUrl("vue-bundle.js"),
                    StyleUrl = HasAsset("vue-bundle.css") ? GetAssetUrl("vue-bundle.css") : null
                });
            }

            // 检查 WebComponent 组件包 (约定)
            if (HasAsset("web-bundle.js"))
            {
                assets.Add(new DynamicComponentAsset
                {
                    PluginId = plugin.Metadata.Id,
                    ComponentType = "WebComponent",
                    ScriptUrl = GetAssetUrl("web-bundle.js"),
                    StyleUrl = HasAsset("web-bundle.css") ? GetAssetUrl("web-bundle.css") : null
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
                string schemaJson = YaeSchemaExporter.GenerateSchemaJson(type, settings =>
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


    /// <summary>
    /// 根据符文类型名，权威地创建一个新的、包含所有默认值的符文配置实例。
    /// 此端点是前端新建任何符文的【唯一】入口，它解决了默认值（包括[DefaultValue]特性）覆盖的核心问题。
    /// </summary>
    /// <param name="runeTypeName">符文的类型名称，例如 "StaticVariableRuneConfig"。</param>
    /// <returns>一个强类型的、已填好所有默认值的全新符文配置对象。</returns>
    /// <response code="200">成功返回了默认配置的实例。</response>
    /// <response code="404">未找到指定的符文类型。</response>
    /// <response code="500">在实例化或处理默认值时发生内部错误。</response>
    [HttpGet("new-rune/{runeTypeName}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AbstractRuneConfig), StatusCodes.Status200OK)] // 返回类型已更新
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<AbstractRuneConfig> GetNewRuneConfig(string runeTypeName)
    {
        var runeType = RuneConfigTypeResolver.FindRuneConfigType(runeTypeName);
        if (runeType is null)
        {
            return this.NotFound($"未找到名为 '{runeTypeName}' 的符文类型。");
        }

        var jsonOptions = YaeSandBoxJsonHelper.JsonSerializerOptions;

        try
        {
            // --- 步骤 1: 建立 C# 初始化器设置的默认值 ---
            // 创建基础实例以捕获属性初始化器 (例如: `... = true;`)。
            object baseInstance = Activator.CreateInstance(runeType)
                                  ?? throw new InvalidOperationException($"无法创建类型 '{runeType.FullName}' 的实例。");

            // 将基础实例序列化为 JSON，然后解析为 JsonDocument。这是我们健壮的、只读的数据源。
            using JsonDocument baseDoc = JsonSerializer.SerializeToDocument(baseInstance, jsonOptions);

            // --- 步骤 2: 建立 [DefaultValue] 特性定义的覆盖值 ---
            // 这个字典将持有我们的最高优先级默认值。
            var defaultValueOverrides = new Dictionary<string, object?>();
            var properties = runeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                var defaultValueAttr = prop.GetCustomAttribute<DefaultValueAttribute>();
                // 我们只关心特性中实际存在的值
                if (defaultValueAttr?.Value is not null)
                {
                    // 获取属性在 JSON 中应有的名称（例如，遵循 camelCase 策略）
                    string jsonPropertyName = jsonOptions.PropertyNamingPolicy?.ConvertName(prop.Name) ?? prop.Name;
                    defaultValueOverrides[jsonPropertyName] = defaultValueAttr.Value;
                }
            }

            // --- 步骤 3: 融合两种来源的默认值 ---
            // 我们在一个新的可变字典中构建最终的 JSON 属性集合。
            // 首先填入优先级最高的 [DefaultValue] 覆盖值。
            var finalJsonProperties = new Dictionary<string, object?>(defaultValueOverrides);

            // 然后，遍历基础实例的 JSON 属性。
            // 如果某个属性没有被 [DefaultValue] 覆盖，我们就从基础实例中添加它。
            // `TryAdd` 方法完美地实现了这个逻辑。
            foreach (var jsonProperty in baseDoc.RootElement.EnumerateObject())
            {
                // 从 JsonDocument 中获取的值必须被 Clone，因为 JsonDocument 将在 using 块结束时被销毁。
                finalJsonProperties.TryAdd(jsonProperty.Name, jsonProperty.Value.Clone());
            }

            // --- 步骤 4: 将最终的属性集合反序列化为强类型对象 ---
            // 将我们精心融合的字典重新序列化为完美的 JSON 字符串。
            string finalJson = JsonSerializer.Serialize(finalJsonProperties, jsonOptions);

            // 将这个完美的 JSON 反序列化回具体的符文类型。这是通往最终胜利的最后一步。
            if (JsonSerializer.Deserialize(finalJson, runeType, jsonOptions) is not AbstractRuneConfig finalInstance)
            {
                throw new JsonException($"在将最终合并的 JSON 反序列化回类型 '{runeType.FullName}' 时失败。");
            }

            return this.Ok(finalInstance);
        }
        catch (Exception ex)
        {
            return this.StatusCode(StatusCodes.Status500InternalServerError,
                $"为类型 '{runeTypeName}' 创建新实例时发生错误: {ex.Message}");
        }
    }
}