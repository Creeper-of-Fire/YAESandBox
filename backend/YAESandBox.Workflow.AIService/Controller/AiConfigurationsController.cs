using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.AIService.AiConfigSchema;
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

    private IAiConfigurationManager configurationManager { get; } = configurationManager;
    private IHttpClientFactory httpClientFactory { get; } = httpClientFactory;

    /// <summary>
    /// 获取所有已保存的 AI 配置集的完整列表。
    /// </summary>
    /// <returns>包含所有 AI 配置集的字典，键为 UUID，值为配置集对象。</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, AiConfigurationSet>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyDictionary<string, AiConfigurationSet>>> GetAllConfigurations()
    {
        var result = await this.configurationManager.GetAllConfigurationsAsync();
        if (result.IsSuccess)
            return this.Ok(result.Value);

        return this.Get500ErrorResult(result, "获取所有已保存的 AI 配置集的完整列表时发生内部错误。");
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

        var result = await this.configurationManager.GetConfigurationByUuidAsync(uuid);
        if (result.IsSuccess)
            return this.Ok(result.Value);

        if (result.HasError<AIConfigError>(e => e.Message.Contains("未找到")))
            return this.NotFound(result.Errors.First().Message);

        return this.Get500ErrorResult(result, "获取配置集时发生内部错误。");
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

        var result = await this.configurationManager.AddConfigurationAsync(configs);
        if (result.IsSuccess)
        {
            // 返回 201 Created，并在 Location header 指向新创建的资源 (可选但推荐)
            // 以及在响应体中返回 UUID
            return this.CreatedAtAction(nameof(this.GetConfigurationByUuid), new { uuid = result.Value }, result.Value);
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
        var existingConfigResult = await this.configurationManager.GetConfigurationByUuidAsync(uuid);
        if (existingConfigResult.IsFailed)
        {
            if (existingConfigResult.HasError<AIConfigError>(e => e.Message.Contains("未找到")))
                return this.NotFound($"未找到 UUID 为 '{uuid}' 的配置集。");

            return this.StatusCode(StatusCodes.Status500InternalServerError, "更新配置前检查配置集失败。");
        }

        if (!this.ModelState.IsValid)
            return this.BadRequest(this.ModelState);

        var updateResult = await this.configurationManager.UpdateConfigurationAsync(uuid, config);
        if (updateResult.IsSuccess)
            return this.NoContent(); // HTTP 204 表示成功处理请求，且响应中无内容主体

        // UpdateConfigurationAsync 内部可能已处理 "未找到" 的情况，这里可能不需要重复检查 NotFound
        // 但如果 Manager 返回了特定的错误，可以据此返回不同的状态码
        return this.Get500ErrorResult(updateResult, "更新配置集时发生内部错误。");
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

        var result = await this.configurationManager.DeleteConfigurationAsync(uuid);
        if (result.IsSuccess)
            return this.NoContent();

        return this.Get500ErrorResult(result, "删除配置集时发生内部错误。");
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
        var httpClient = this.httpClientFactory.CreateClient();

        var dependencies = new AiProcessorDependencies(httpClient);

        var type = ConfigSchemasHelper.GetTypeByName(moduleType);


        if (type == null || !typeof(AbstractAiProcessorConfig).IsAssignableFrom(type))
        {
            return this.NotFound(
                $"Unknown or invalid ModuleType '{moduleType}' found as dictionary key. Cannot determine concrete type for AbstractAiProcessorConfig.");
        }

        var config = (AbstractAiProcessorConfig?)testAiDto.configJson.Deserialize(type, YAESandBoxJsonHelper.JsonSerializerOptions);
        if (config == null)
            return this.NotFound();

        var result = await config.ToAiProcessor(dependencies).NonStreamRequestAsync([(PromptRole.System(), testAiDto.testText)]);
        if (result.IsSuccess)
            return this.Ok(result.Value);

        return this.Get500ErrorResult(result, "生成测试文本时发生内部错误。");
    }

    /// <summary>
    /// 测试用DTO
    /// </summary>
    /// <param name="configJson">测试的Config，序列化后的AbstractAiProcessorConfig</param>
    /// <param name="testText">测试文本</param>
    public record TestAiDto([Required]JsonDocument configJson,[Required] string testText);

    private ObjectResult Get500ErrorResult<T>(Result<T> result, string defaultMessage) =>
        this.Get500ErrorResult(result.ToResult(), defaultMessage);

    private ObjectResult Get500ErrorResult(Result result, string defaultMessage)
    {
        if (result.IsSuccess)
            throw new UnreachableException($"GetErrorResult: {result} 必定是失败的，但却输入了成功的情况。");
        return this.StatusCode(StatusCodes.Status500InternalServerError, result.Errors.FirstOrDefault()?.Message ?? defaultMessage);
    }
}