using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.Schema;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.AIService.ConfigManagement;

namespace YAESandBox.Workflow.AIService.Controller;

[ApiExplorerSettings(GroupName = AiConfigGroupName)]
[ApiController]
[Route("api/ai-configurations")]
public class AiConfigurationsController(IAiConfigurationManager configurationManager, IHttpClientFactory httpClientFactory) : ControllerBase
{
    /// <summary>
    /// Api文档的GroupName
    /// </summary>
    public const string AiConfigGroupName = "aiconfig";

    private IAiConfigurationManager ConfigurationManager { get; } = configurationManager;
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;

    /// <summary>
    /// 获取所有已保存的 AI 配置集的完整列表。
    /// </summary>
    /// <returns>包含所有 AI 配置集的字典，键为 UUID，值为配置集对象。</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, AiConfigurationSet>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyDictionary<string, AiConfigurationSet>>> GetAllConfigurations()
    {
        return await this.ConfigurationManager.GetAllConfigurationsAsync().ToActionResultAsync();
    }

    /// <summary>
    /// 根据 UUID 获取一个特定的 AI 配置集。
    /// </summary>
    /// <param name="uuid">配置集的唯一标识符。</param>
    /// <returns>找到的 AI 配置集对象。</returns>
    [HttpGet("{uuid}")]
    [ProducesResponseType(typeof(AiConfigurationSet), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AiConfigurationSet>> GetConfigurationByUuid(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return this.BadRequest("UUID 不能为空。");

        return await this.ConfigurationManager.GetConfigurationByUuidAsync(uuid).ToActionResultAsync();
    }

    /// <summary>
    /// 添加一个新的 AI 配置集。
    /// </summary>
    /// <param name="configs">要添加的 AI 配置集对象。</param>
    /// <returns>新创建配置集的 UUID。</returns>
    [HttpPost]
    [ProducesResponseType(typeof(string), StatusCodes.Status201Created)] // 返回新资源的 UUID
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string>> AddConfiguration([FromBody] AiConfigurationSet configs)
    {
        // 模型绑定器和 JSON 反序列化器会根据请求体中的 "ModuleType" 字段
        // 自动将 JSON 反序列化为正确的 AbstractAiProcessorConfig 子类型 (如 DoubaoAiProcessorConfig)。

        if (!this.ModelState.IsValid) // 检查数据注解验证 (如 [Required] 在 AbstractAiProcessorConfig 或其子类上)
            return this.BadRequest(this.ModelState);

        var result = await this.ConfigurationManager.AddConfigurationAsync(configs);
        if (result.TryGetValue(out string? value))
        {
            return this.CreatedAtAction(nameof(this.GetConfigurationByUuid), new { uuid = value }, value);
        }

        return this.Get500ErrorResult(result, "添加配置集时发生内部错误。");
    }

    /// <summary>
    /// 更新一个已存在的 AI 配置集。
    /// </summary>
    /// <param name="uuid">要更新的配置集的唯一标识符。</param>
    /// <param name="config">包含更新信息的 AI 配置集对象。</param>
    /// <returns>无内容响应表示成功。</returns>
    [HttpPut("{uuid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)] // 成功更新，无内容返回
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateConfiguration(string uuid, [FromBody] AiConfigurationSet config)
    {
        if (string.IsNullOrWhiteSpace(uuid))
        {
            return this.BadRequest("UUID 不能为空。");
        }

        // 验证：传入的 config 的 ModuleType 应该与存储中 uuid 对应的配置的 ModuleType 一致。
        // 这一步可以在 Manager 层做，或者在这里做。
        var existingConfigResult = await this.ConfigurationManager.GetConfigurationByUuidAsync(uuid);
        if (existingConfigResult.IsFailed)
        {
            if (existingConfigResult.HasError<AiConfigError>(e => e.Message.Contains("未找到")))
                return this.NotFound($"未找到 UUID 为 '{uuid}' 的配置集。");

            return this.StatusCode(StatusCodes.Status500InternalServerError, "更新配置前检查配置集失败。");
        }

        if (!this.ModelState.IsValid)
            return this.BadRequest(this.ModelState);

        return await this.ConfigurationManager.UpdateConfigurationAsync(uuid, config).ToActionResultAsync();
    }

    /// <summary>
    /// 根据 UUID 删除一个 AI 配置集。
    /// </summary>
    /// <param name="uuid">要删除的配置集的唯一标识符。</param>
    /// <returns>无内容响应表示成功（即使配置原先不存在，删除也是幂等的）。</returns>
    [HttpDelete("{uuid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteConfiguration(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return this.BadRequest("UUID 不能为空。");

        return await this.ConfigurationManager.DeleteConfigurationAsync(uuid).ToActionResultAsync();
    }

    /// <summary>
    /// 测试Ai配置
    /// </summary>
    /// <param name="moduleType">配置的类型。</param>
    /// <param name="testAiDto">配置和测试文本。</param>
    /// <returns></returns>
    [HttpPost("ai-config-test/{moduleType}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string>> TestAiConfig(string moduleType, [FromBody] TestAiDto testAiDto)
    {
        var httpClient = this.HttpClientFactory.CreateClient();

        var dependencies = new AiProcessorDependencies(httpClient);

        var type = ConfigSchemasHelper.GetTypeByName(moduleType);


        if (type == null || !typeof(AbstractAiProcessorConfig).IsAssignableFrom(type))
        {
            return this.NotFound(
                $"Unknown or invalid ModuleType '{moduleType}' found as dictionary key. Cannot determine concrete type for AbstractAiProcessorConfig.");
        }

        var config = (AbstractAiProcessorConfig?)testAiDto.ConfigJson.Deserialize(type, YaeSandBoxJsonHelper.JsonSerializerOptions);
        if (config == null)
            return this.NotFound();

        return await config.ToAiProcessor(dependencies)
            .NonStreamRequestAsync((List<RoledPromptDto>) [RoledPromptDto.System(testAiDto.TestText)]).ToActionResultAsync();
    }

    /// <summary>
    /// 获取所有可用的 AI 配置【类型定义】列表。
    /// 用于前端展示可以【新建】哪些类型的 AI 配置。
    /// </summary>
    /// <returns>一个列表，每个 SelectOptionDto 包含：
    /// 'Value': 配置类型的编程名称 (如 "DoubaoAiProcessorConfig")，用于后续请求 Schema。
    /// 'Label': 用户友好的类型显示名称 (如 "豆包AI模型")。
    /// </returns>
    [HttpGet("available-config-types")]
    [ProducesResponseType(typeof(List<SelectOptionDto>), StatusCodes.Status200OK)] // SelectOptionDto 来自 Schema 命名空间
    public ActionResult<List<SelectOptionDto>> GetAvailableConfigTypeDefinitions()
    {
        var availableTypes = ConfigSchemasHelper.GetAvailableAiConfigConcreteTypes();

        var result = availableTypes.Select(type =>
            {
                // 尝试从 DisplayAttribute 获取用户友好的名称
                var displayAttr = type.GetCustomAttribute<DisplayAttribute>(false); // false: 不检查继承链
                string label = displayAttr?.GetName() ?? type.Name;

                // 简化标签名称的逻辑 (与你之前提供的 AiConfigSchemasController 类似)
                if (label.EndsWith("AiProcessorConfig", StringComparison.OrdinalIgnoreCase))
                {
                    label = label[..^"AiProcessorConfig".Length].Trim();
                }
                else if (label.EndsWith("Config", StringComparison.OrdinalIgnoreCase))
                {
                    label = label[..^"Config".Length].Trim();
                }

                if (string.IsNullOrEmpty(label)) label = type.Name; // Fallback

                return new SelectOptionDto { Value = type.Name, Label = label };
            })
            .OrderBy(so => so.Label)
            .ToList();

        return this.Ok(result);
    }

    /// <summary>
    /// 获取指定 AI 模块类型的配置模板，包含初始默认数据和可选的 JSON Schema。
    /// 用于前端为新配置项生成表单。
    /// </summary>
    /// <param name="moduleType">AI 模块的类型名称 (例如 "DoubaoAiProcessorConfig")。</param>
    /// <param name="includeSchema">是否在响应中包含 JSON Schema。默认为 true。</param>
    /// <returns>一个包含初始数据和可选 Schema 的对象。</returns>
    [HttpGet("templates/{moduleType}")]
    [ProducesResponseType(typeof(DataWithSchemaDto<AbstractAiProcessorConfig>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<DataWithSchemaDto<AbstractAiProcessorConfig>> GetConfigurationTemplate(
        string moduleType, [FromQuery] bool includeSchema = false)
    {
        if (string.IsNullOrWhiteSpace(moduleType))
        {
            return this.BadRequest("AI 模块类型名称 (moduleType) 不能为空。");
        }

        var configType = ConfigSchemasHelper.GetTypeByName(moduleType);

        if (configType == null)
            return this.NotFound($"名为 '{moduleType}' 的 AI 模块类型未找到。");

        if (!typeof(AbstractAiProcessorConfig).IsAssignableFrom(configType))
            // 理论上不会发生，ConfigSchemasHelper.GetTypeByName 可能已经处理
            return this.BadRequest($"类型 '{moduleType}' 不是一个有效的 AI 配置类型。");


        // 1. 生成 initialData
        // 尝试创建该类型的一个实例作为基础初始数据
        // Activator.CreateInstance 要求类型有无参数构造函数

        if (Activator.CreateInstance(configType) is not AbstractAiProcessorConfig initialData)
        {
            // 如果 Activator.CreateInstance 失败 (例如没有无参构造函数)，这是一个问题
            // 或者你的 AbstractAiProcessorConfig 基类或子类构造函数有特殊逻辑阻止了简单实例化
            return this.StatusCode(StatusCodes.Status500InternalServerError, $"无法为类型 '{moduleType}' 创建初始数据实例。请确保它有一个公共无参数构造函数。");
        }

        // TODO: 在这里根据 moduleType 或 initialData 的具体类型，填充需要从外部引入的默认值
        // 示例：处理 APIKEY (假设你的 AbstractAiProcessorConfig 或其子类有 ApiKey 属性)
        // if (initialData is IRequiresApiKey apiKeyConfig) // 假设有一个接口 IRequiresApiKey { string ApiKey { get; set; } }
        // {
        //     // 从配置、环境变量或其他服务获取默认 API Key
        //     // 注意：这里的 "YourExternalApiKeyProvider" 仅为示例，你需要替换为实际的获取逻辑
        //     var defaultApiKey = Environment.GetEnvironmentVariable($"DEFAULT_API_KEY_{moduleType.ToUpperInvariant()}")
        //                         ?? this.configurationManager.GetDefaultApiKeyForType(moduleType); // 假设 Manager 有此方法
        //
        //     if (!string.IsNullOrEmpty(defaultApiKey))
        //     {
        //         apiKeyConfig.ApiKey = defaultApiKey;
        //     }
        //     else
        //     {
        //         // 如果没有找到外部默认值，可以设置一个占位符或提示
        //         apiKeyConfig.ApiKey = "请在此处输入您的API Key";
        //     }
        // }

        return DataWithSchemaDto<AbstractAiProcessorConfig>.Create(initialData).ToActionResult();
    }

    /// <summary>
    /// 代表一个选择项，用于下拉列表或单选/复选按钮组。
    /// </summary>
    public class SelectOptionDto
    {
        /// <summary>
        /// 选项的实际值。
        /// </summary>
        [Required]
        public object Value { get; init; } = string.Empty;

        /// <summary>
        /// 选项在UI上显示的文本。
        /// </summary>
        [Required]
        public string Label { get; init; } = string.Empty;
    }

    /// <summary>
    /// 测试用DTO
    /// </summary>
    /// <param name="ConfigJson">测试的Config，序列化后的AbstractAiProcessorConfig</param>
    /// <param name="TestText">测试文本</param>
    public record TestAiDto([Required] JsonDocument ConfigJson, [Required] string TestText);
}