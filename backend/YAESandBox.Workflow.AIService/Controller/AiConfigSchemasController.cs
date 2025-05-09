using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.AIService.AiConfigSchema;
using YAESandBox.Workflow.AIService.ConfigManagement;

namespace YAESandBox.Workflow.AIService.Controller;

[ApiController]
[Route("api/ai-configuration-management")] // 更改路由以反映其更广泛的职责（如果合并了实例列表）或保持 schema 特定
public class AiConfigSchemasController(IAiConfigurationManager configurationManager) : ControllerBase
{
    private IAiConfigurationManager configurationManager { get; } = configurationManager;

    /// <summary>
    /// 获取指定 AI 配置类型的表单 Schema 结构。
    /// 用于前端动态生成该类型配置的【新建】或【编辑】表单骨架。
    /// </summary>
    /// <param name="configTypeName">AI 配置的类型名称 (例如 "DoubaoAiProcessorConfig")。</param>
    /// <returns>包含该类型所有可配置属性的 FormFieldSchema 列表。</returns>
    [HttpGet("schemas/{configTypeName}")]
    [ProducesResponseType(typeof(List<FormFieldSchema>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<List<FormFieldSchema>> GetSchemaForConfigType(string configTypeName)
    {
        if (string.IsNullOrWhiteSpace(configTypeName))
        {
            return this.BadRequest("配置类型名称 (configTypeName) 不能为空。");
        }

        Type? configType = ConfigSchemasBuildHelper.GetTypeByName(configTypeName);

        if (configType == null)
        {
            return this.NotFound($"名为 '{configTypeName}' 的 AI 配置类型未找到。");
        }

        // GetTypeByName 已经确保了它是 AbstractAiProcessorConfig 的子类，但可以再加一层防御
        if (!typeof(AbstractAiProcessorConfig).IsAssignableFrom(configType))
        {
            return this.BadRequest($"类型 '{configTypeName}' 不是一个有效的 AI 配置类型。");
        }

        try
        {
            var schema = ConfigSchemasBuildHelper.GenerateSchemaForType(configType);
            return this.Ok(schema);
        }
        catch (Exception ex)
        {
            return this.StatusCode(StatusCodes.Status500InternalServerError, $"为类型 '{configTypeName}' 生成 Schema 时发生内部错误。");
        }
    }

    /// <summary>
    /// 获取所有可用的 AI 配置【类型定义】列表。
    /// 用于前端展示可以【新建】哪些类型的 AI 配置。
    /// </summary>
    /// <returns>一个列表，每个 SelectOption 包含：
    /// 'Value': 配置类型的编程名称 (如 "DoubaoAiProcessorConfig")，用于后续请求 Schema。
    /// 'Label': 用户友好的类型显示名称 (如 "豆包AI模型")。
    /// </returns>
    [HttpGet("available-config-types")]
    [ProducesResponseType(typeof(List<SelectOption>), StatusCodes.Status200OK)]
    public ActionResult<List<SelectOption>> GetAvailableConfigTypeDefinitions()
    {
        var availableTypes = ConfigSchemasBuildHelper.GetAvailableAiConfigConcreteTypes();

        var result = availableTypes.Select(type =>
            {
                var displayAttr = type.GetCustomAttribute<DisplayAttribute>();
                string label = displayAttr?.GetName() ?? type.Name; // 尝试获取本地化名称
                
                if (label.EndsWith("AiProcessorConfig", StringComparison.OrdinalIgnoreCase))
                {
                    label = label.Substring(0, label.Length - "AiProcessorConfig".Length).Trim();
                }
                else if (label.EndsWith("Config", StringComparison.OrdinalIgnoreCase))
                {
                    label = label.Substring(0, label.Length - "Config".Length).Trim();
                }

                if (string.IsNullOrEmpty(label)) label = type.Name;

                // 对于类型选择，Value 是类型本身的名称
                return new SelectOption { Value = type.Name, Label = label };
            })
            .OrderBy(so => so.Label)
            .ToList();

        return this.Ok(result);
    }

    /// <summary>
    /// 获取所有【已保存的 AI 配置实例】的摘要列表。
    /// 用于前端生成一个下拉框或列表，让用户选择一个已存在的配置进行【编辑】或【使用】。
    /// </summary>
    /// <returns>一个列表，每个 SelectOption 包含：
    /// 'Value': 配置实例的唯一标识符 (UUID)。
    /// 'Label': 配置实例的用户定义名称 (ConfigName)。
    /// </returns>
    [HttpGet("saved-configurations")]
    [ProducesResponseType(typeof(List<SelectOption>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<SelectOption>>> GetSavedConfigurationInstances()
    {
        var allConfigsResult = await this.configurationManager.GetAllConfigurationsAsync();

        if (allConfigsResult.IsFailed)
        {
            return this.StatusCode(StatusCodes.Status500InternalServerError, "获取已保存配置列表时发生错误。");
        }

        if (allConfigsResult.Value == null || !allConfigsResult.Value.Any())
        {
            return this.Ok(new List<SelectOption>());
        }

        var selectOptions = allConfigsResult.Value
            .Select(kvp => new SelectOption
            {
                Value = kvp.Key, // UUID 作为 Value
                Label = string.IsNullOrWhiteSpace(kvp.Value.ConfigName)
                    ? $"配置 ({kvp.Value.ModuleType}, UUID: {kvp.Key.Substring(0, 8)}...)"
                    : kvp.Value.ConfigName
            })
            .OrderBy(so => so.Label)
            .ToList();

        return this.Ok(selectOptions);
    }
}