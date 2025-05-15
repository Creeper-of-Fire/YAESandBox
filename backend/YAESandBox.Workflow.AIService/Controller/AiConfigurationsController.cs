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
    /// 获取指定 AI 模块类型的初始默认数据。
    /// 用于前端为新配置项生成表单。
    /// </summary>
    /// <param name="moduleType">AI 模块的类型名称 (例如 "DoubaoAiProcessorConfig")。</param>
    /// <returns>初始数据。</returns>
    [HttpGet("default-data/{moduleType}")]
    [ProducesResponseType(typeof(AbstractAiProcessorConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<AbstractAiProcessorConfig> GetConfigurationDefaultData(string moduleType)
    {
        if (string.IsNullOrWhiteSpace(moduleType))
            return this.BadRequest("AI 模块类型名称 (moduleType) 不能为空。");

        var configType = ConfigSchemasHelper.GetTypeByName(moduleType);

        if (configType == null)
            return this.NotFound($"名为 '{moduleType}' 的 AI 模块类型未找到。");

        if (!typeof(AbstractAiProcessorConfig).IsAssignableFrom(configType))
            // 理论上不会发生，ConfigSchemasHelper.GetTypeByName 可能已经处理
            return this.BadRequest($"类型 '{moduleType}' 不是一个有效的 AI 配置类型。");
        
        if (Activator.CreateInstance(configType) is not AbstractAiProcessorConfig initialData)
            return this.StatusCode(StatusCodes.Status500InternalServerError, $"无法为类型 '{moduleType}' 创建初始数据实例。请确保它有一个公共无参数构造函数。");

        return this.Ok(initialData);
    }

    /// <summary>
    /// 测试用DTO
    /// </summary>
    /// <param name="ConfigJson">测试的Config，序列化后的AbstractAiProcessorConfig</param>
    /// <param name="TestText">测试文本</param>
    public record TestAiDto([Required] JsonDocument ConfigJson, [Required] string TestText);
}