﻿using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
public class AiConfigurationsController(IAiConfigurationManager configurationManager, IHttpClientFactory httpClientFactory) : ControllerBase
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
        return await this.ConfigurationManager.GetAllConfigurationsAsync().ToActionResultAsync();
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

        return await this.ConfigurationManager.GetConfigurationByUuidAsync(uuid).ToActionResultAsync();
    }

    /// <summary>
    /// 添加一个新的 AI 配置集。
    /// </summary>
    /// <param name="configs">要添加的 AI 配置集对象。</param>
    /// <returns>新创建配置集的 UUID。</returns>
    /// <response code="201">配置集已成功创建，并返回新创建的 UUID。</response>
    /// <response code="400">请求体无效或模型验证失败。</response>
    /// <response code="500">添加配置集时发生内部服务器错误。</response>
    [HttpPost]
    [ProducesResponseType(typeof(string), StatusCodes.Status201Created)] // 返回新资源的 UUID
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string>> AddConfiguration([FromBody] AiConfigurationSet configs)
    {
        var result = await this.ConfigurationManager.AddConfigurationAsync(configs);
        if (result.TryGetValue(out string? value))
        {
            return this.CreatedAtAction(nameof(this.GetConfigurationByUuid), new { uuid = value }, value);
        }

        return this.Get500ErrorResult(result);
    }

    /// <summary>
    /// 更新一个已存在的 AI 配置集。
    /// </summary>
    /// <param name="uuid">要更新的配置集的唯一标识符。</param>
    /// <param name="config">包含更新信息的 AI 配置集对象。</param>
    /// <returns>无内容响应表示成功。</returns>
    /// <response code="204">配置集已成功更新。</response>
    /// <response code="400">请求无效，例如 UUID 为空。</response>
    /// <response code="404">未找到要更新的配置集。</response>
    /// <response code="500">更新配置时发生内部服务器错误。</response>
    [HttpPut("{uuid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)] // 成功更新，无内容返回
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateConfiguration(string uuid, [FromBody] AiConfigurationSet config)
    {
        // return this.StatusCode(StatusCodes.Status500InternalServerError, "更新配置前检查配置集失败。"); // 测试用
        if (string.IsNullOrWhiteSpace(uuid))
            return this.BadRequest("UUID 不能为空。");

        // 验证：传入的 config 的 ModuleType 应该与存储中 uuid 对应的配置的 ModuleType 一致。
        // 这一步可以在 Manager 层做，或者在这里做。
        var existingConfigResult = await this.ConfigurationManager.GetConfigurationByUuidAsync(uuid);
        if (existingConfigResult.TryGetError(out var error))
            return this.Get500ErrorResult(error);

        return await this.ConfigurationManager.UpdateConfigurationAsync(uuid, config).ToActionResultAsync();
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

        return await this.ConfigurationManager.DeleteConfigurationAsync(uuid).ToActionResultAsync();
    }

    /// <summary>
    /// 测试Ai配置
    /// </summary>
    /// <param name="testAiDto">配置和测试文本。</param>
    /// <returns></returns>
    /// <response code="200">AI 配置测试成功，返回 AI 生成的完整文本。</response>
    /// <response code="500">测试期间发生错误，例如 AI 服务调用失败。</response>
    [HttpPost("ai-config-test/{moduleType}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string>> TestAiConfig([FromBody] TestAiDto testAiDto)
    {
        var httpClient = this.HttpClientFactory.CreateClient();

        var dependencies = new AiProcessorDependencies(httpClient);

        return await testAiDto.ConfigJson.ToAiProcessor(dependencies)
            .NonStreamRequestAsync((List<RoledPromptDto>) [RoledPromptDto.System(testAiDto.TestText)]).ToActionResultAsync();
    }

    /// <summary>
    /// 获取指定 AI 模块类型的初始默认数据。
    /// 用于前端为新配置项生成表单。
    /// </summary>
    /// <param name="moduleType">AI 模块的类型名称 (例如 "DoubaoAiProcessorConfig")。</param>
    /// <returns>初始数据。</returns>
    /// <response code="200">成功获取指定 AI 模块类型的默认数据。</response>
    /// <response code="400">请求的模块类型名称无效。</response>
    /// <response code="404">未找到指定名称的 AI 模块类型。</response>
    /// <response code="500">获取默认数据时发生内部服务器错误。</response>
    [HttpGet("default-data/{moduleType}")]
    [ProducesResponseType(typeof(AbstractAiProcessorConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Obsolete("我们希望前端创建空对象并且实现加载默认值的功能。")]
    public async Task<ActionResult<AbstractAiProcessorConfig>> GetConfigurationDefaultData(string moduleType)
    {
        if (string.IsNullOrWhiteSpace(moduleType))
            return this.BadRequest("AI 模块类型名称 (moduleType) 不能为空。");

        var configType = ConfigSchemasHelper.GetAiConfigTypeByName(moduleType);

        if (configType == null)
            return this.NotFound($"名为 '{moduleType}' 的 AI 模块类型未找到。");

        if (!typeof(AbstractAiProcessorConfig).IsAssignableFrom(configType))
            // 理论上不会发生，ConfigSchemasHelper.GetTypeByName 可能已经处理
            return this.BadRequest($"类型 '{moduleType}' 不是一个有效的 AI 配置类型。");

        var allSetsResult = await this.ConfigurationManager.GetAllConfigurationsAsync();
        if (allSetsResult.TryGetValue(out var allSets))
        {
            var set = allSets.FirstOrDefault(s => s.Value.ConfigSetName == AiConfigurationSet.DefaultConfigSetName).Value;
            if (set != null)
            {
                if (set.Configurations.TryGetValue(moduleType, out var defaultConfig))
                {
                    object? configWithRequiredOnly = YaeSandBoxJsonHelper.CreateObjectWithRequiredPropertiesOnly(defaultConfig, configType);
                    if (configWithRequiredOnly is AbstractAiProcessorConfig aiProcessorConfig)
                        return this.Ok(aiProcessorConfig);
                }
            }
        }

        if (Activator.CreateInstance(configType) is not AbstractAiProcessorConfig initialData)
            return this.StatusCode(StatusCodes.Status500InternalServerError, $"无法为类型 '{moduleType}' 创建初始数据实例。请确保它有一个公共无参数构造函数。");

        return this.Ok(initialData);
    }

    /// <summary>
    /// 测试用DTO
    /// </summary>
    /// <param name="ConfigJson">AbstractAiProcessorConfig</param>
    /// <param name="TestText">测试文本</param>
    public record TestAiDto([Required] AbstractAiProcessorConfig ConfigJson, [Required] string TestText);
}