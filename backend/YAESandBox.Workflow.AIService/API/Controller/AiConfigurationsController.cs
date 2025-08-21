using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.AIService.ConfigManagement;

namespace YAESandBox.Workflow.AIService.API.Controller;

/// <summary>
/// 管理 AI 配置集（AiConfigurationSet）的 CRUD 操作和相关功能（如配置测试）。
/// </summary>
/// <param name="configurationManager">AI 配置管理器服务，用于处理配置的持久化。</param>
/// <param name="httpClientFactory">用于创建 HttpClient 实例，以进行 AI 配置测试。</param>
[ApiExplorerSettings(GroupName = AiServiceConfigModule.AiConfigGroupName)]
[ApiController]
[Route("api/ai-configurations")]
public class AiConfigurationsController(IAiConfigurationManager configurationManager, IHttpClientFactory httpClientFactory)
    : AuthenticatedApiControllerBase
{
    private IAiConfigurationManager ConfigurationManager { get; } = configurationManager;
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;

    /// <summary>
    /// 获取所有已保存的 AI 配置集的完整列表。
    /// </summary>
    /// <returns>包含所有 AI 配置集的字典，键为 UUID，值为配置集对象。</returns>
    /// <response code="200">成功获取所有 AI 配置集的列表。</response>
    /// <response code="500">获取配置时发生内部服务器错误。</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, AiConfigurationSet>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyDictionary<string, AiConfigurationSet>>> GetAllConfigurations()
    {
        return await this.ConfigurationManager.GetAllConfigurationsAsync(this.UserId).ToActionResultAsync();
    }

    /// <summary>
    /// 根据 UUID 获取一个特定的 AI 配置集。
    /// </summary>
    /// <param name="uuid">配置集的唯一标识符。</param>
    /// <returns>找到的 AI 配置集对象。</returns>
    /// <response code="200">成功获取指定的 AI 配置集。</response>
    /// <response code="400">请求的 UUID 无效（例如，为空）。</response>
    /// <response code="404">未找到指定 UUID 的配置集。</response>
    [HttpGet("{uuid}")]
    [ProducesResponseType(typeof(AiConfigurationSet), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiConfigurationSet>> GetConfigurationByUuid(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return this.BadRequest("UUID 不能为空。");

        return await this.ConfigurationManager.GetConfigurationByUuidAsync(this.UserId, uuid).ToActionResultAsync();
    }

    /// <summary>
    /// 创建或更新一个 AI 配置集。此操作是幂等的。
    /// </summary>
    /// <param name="uuid">要创建或更新的配置集的唯一标识符（由客户端提供）。</param>
    /// <param name="config">包含完整信息的 AI 配置集对象。</param>
    /// <returns>如果创建了新配置，返回 201 Created；如果更新了现有配置，返回 204 No Content。</returns>
    /// <response code="201">配置集已成功创建。</response>
    /// <response code="204">配置集已成功更新。</response>
    /// <response code="400">请求无效，例如 UUID 为空或请求体无效。</response>
    /// <response code="500">保存配置时发生内部服务器错误。</response>
    [HttpPut("{uuid}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpsertConfiguration(string uuid, [FromBody] AiConfigurationSet config)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return this.BadRequest("UUID 不能为空。");

        var result = await this.ConfigurationManager.UpsertConfigurationAsync(this.UserId, uuid, config);

        if (result.TryGetError(out var error, out var upsertType))
        {
            return this.Get500ErrorResult(error);
        }

        return upsertType == UpsertResultType.Created
            ? this.CreatedAtAction(nameof(this.GetConfigurationByUuid), new { uuid = uuid }, config)
            : this.NoContent();
    }

    /// <summary>
    /// 根据 UUID 删除一个 AI 配置集。
    /// </summary>
    /// <param name="uuid">要删除的配置集的唯一标识符。</param>
    /// <returns>无内容响应表示成功（即使配置原先不存在，删除也是幂等的）。</returns>
    /// <response code="204">配置集已成功删除（或原先就不存在）。</response>
    /// <response code="400">请求的 UUID 无效。</response>
    /// <response code="500">删除配置时发生内部服务器错误。</response>
    [HttpDelete("{uuid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteConfiguration(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return this.BadRequest("UUID 不能为空。");

        return await this.ConfigurationManager.DeleteConfigurationAsync(this.UserId, uuid).ToActionResultAsync();
    }

    /// <summary>
    /// 测试Ai配置
    /// </summary>
    /// <param name="testAiDto">配置和测试文本。</param>
    /// <returns></returns>
    /// <response code="200">AI 配置测试成功，返回 AI 生成的完整文本。</response>
    /// <response code="500">测试期间发生错误，例如 AI 服务调用失败。</response>
    [HttpPost("ai-config-test/{aiModelType}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string>> TestAiConfig([FromBody] TestAiDto testAiDto)
    {
        var httpClient = this.HttpClientFactory.CreateClient();

        var dependencies = new AiProcessorDependencies(httpClient);

        // 1. 创建一个 TaskCompletionSource<Result<string>>。
        //    它是一个"承诺"，承诺未来会有一个 Result<string> 类型的最终结果。
        var tcs = new TaskCompletionSource<Result<string>>();

        try
        {
            // 2. 从 DTO 创建 AI 处理器实例
            var aiProcessor = testAiDto.ConfigJson.ToAiProcessor(dependencies);

            // 3. 准备回调对象，将结果设置到 TaskCompletionSource
            var callBack = new NonStreamRequestCallBack
            {
                // 当收到最终响应时，我们兑现承诺，设置 TaskCompletionSource 的结果
                OnFinalResponseReceivedAsync = response =>
                {
                    tcs.TrySetResult(Result.Ok(response.ToLegacyThinkString()));
                    return Task.FromResult(Result.Ok());
                }
            };

            // 4. 发起非流式请求。这个调用现在只返回 Task<Result> 用于传递执行错误。
            var executionResult = await aiProcessor.NonStreamRequestAsync(
                [RoledPromptDto.System(testAiDto.TestText)], // 使用C# 12集合表达式简化
                callBack
            );

            // 5. 检查执行过程本身是否出错 (比如网络问题、认证失败等)
            if (executionResult.TryGetError(out var error))
            {
                // 如果AI处理器执行失败，我们也让 TaskCompletionSource 带着这个失败结果完成。
                tcs.TrySetResult(error);
            }

            // 如果执行成功，此时 OnFinalResponseReceived 回调应该已经被触发，
            // tcs.Task 也应该已经完成了。
        }
        catch (Exception ex)
        {
            // 捕获创建或执行过程中的任何同步/异步异常
            tcs.TrySetResult(Result.Fail($"测试AI配置时发生意外错误: {ex.Message}"));
        }

        // 6. 等待 TaskCompletionSource 的 Task 完成，并获取其结果
        //    这个 await 会一直等到 tcs.TrySetResult() 被调用。
        var finalResult = await tcs.Task;

        // 7. 将最终的 Result<string> 转换为 ActionResult
        return finalResult.ToActionResult();
    }

    /// <summary>
    /// 获取指定 AI 模型类型的初始默认数据。
    /// 用于前端为新配置项生成表单。
    /// </summary>
    /// <param name="aiModelType">AI 模型的类型名称 (例如 "DoubaoAiProcessorConfig")。</param>
    /// <returns>初始数据。</returns>
    /// <response code="200">成功获取指定 AI 模型类型的默认数据。</response>
    /// <response code="400">请求的模型类型名称无效。</response>
    /// <response code="404">未找到指定名称的 AI 模型类型。</response>
    /// <response code="500">获取默认数据时发生内部服务器错误。</response>
    [HttpGet("default-data/{aiModelType}")]
    [ProducesResponseType(typeof(AbstractAiProcessorConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Obsolete("我们希望前端创建空对象并且实现加载默认值的功能。")]
    public async Task<ActionResult<AbstractAiProcessorConfig>> GetConfigurationDefaultData(string aiModelType)
    {
        if (string.IsNullOrWhiteSpace(aiModelType))
            return this.BadRequest("AI 模块类型名称 (aiModelType) 不能为空。");

        var configType = ConfigSchemasHelper.GetAiConfigTypeByName(aiModelType);

        if (configType == null)
            return this.NotFound($"名为 '{aiModelType}' 的 AI 模块类型未找到。");

        if (!typeof(AbstractAiProcessorConfig).IsAssignableFrom(configType))
            // 理论上不会发生，ConfigSchemasHelper.GetTypeByName 可能已经处理
            return this.BadRequest($"类型 '{aiModelType}' 不是一个有效的 AI 配置类型。");

        var allSetsResult = await this.ConfigurationManager.GetAllConfigurationsAsync(this.UserId);
        if (allSetsResult.TryGetValue(out var allSets))
        {
            var set = allSets.FirstOrDefault(s => s.Value.ConfigSetName == AiConfigurationSet.DefaultConfigSetName).Value;
            if (set != null)
            {
                if (set.Configurations.TryGetValue(aiModelType, out var defaultConfig))
                {
                    object? configWithRequiredOnly = YaeSandBoxJsonHelper.CreateObjectWithRequiredPropertiesOnly(defaultConfig, configType);
                    if (configWithRequiredOnly is AbstractAiProcessorConfig aiProcessorConfig)
                        return this.Ok(aiProcessorConfig);
                }
            }
        }

        if (Activator.CreateInstance(configType) is not AbstractAiProcessorConfig initialData)
            return this.StatusCode(StatusCodes.Status500InternalServerError, $"无法为类型 '{aiModelType}' 创建初始数据实例。请确保它有一个公共无参数构造函数。");

        return this.Ok(initialData);
    }

    /// <summary>
    /// 测试用DTO
    /// </summary>
    /// <param name="ConfigJson">AbstractAiProcessorConfig</param>
    /// <param name="TestText">测试文本</param>
    public record TestAiDto([Required] AbstractAiProcessorConfig ConfigJson, [Required] string TestText);
}