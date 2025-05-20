using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Depend.Schema;
using YAESandBox.Workflow.AIService.AiConfig;

namespace YAESandBox.Workflow.AIService.Controller;

[ApiExplorerSettings(GroupName = AiConfigurationsController.AiConfigGroupName)]
[ApiController]
[Route("api/ai-configuration-management")]
public class AiConfigSchemasController : ControllerBase
{
    /// <summary>
    /// 获取指定 AI 配置类型的表单 Schema 结构 (JSON Schema 格式，包含 ui: 指令)。
    /// 用于前端动态生成该类型配置的【新建】或【编辑】表单骨架。
    /// </summary>
    /// <param name="configTypeName">AI 配置的类型名称 (例如 "DoubaoAiProcessorConfig")。</param>
    /// <returns>一个 JSON 字符串，代表符合 JSON Schema 标准并包含 vue-json-schema-form UI 指令的 Schema。</returns>
    [HttpGet("schemas/{configTypeName}")]
    [Produces("application/json")] // 明确指定返回 application/json
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)] // 返回的是 JSON Schema 对象，typeof(object) 比较通用
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<object> GetSchemaForConfigType(string configTypeName) // 返回 ActionResult<object> 以便返回原始 JSON
    {
        if (string.IsNullOrWhiteSpace(configTypeName))
        {
            return this.BadRequest("配置类型名称 (configTypeName) 不能为空。");
        }

        var configType = ConfigSchemasHelper.GetAiConfigTypeByName(configTypeName);

        if (configType == null)
        {
            return this.NotFound($"名为 '{configTypeName}' 的 AI 配置类型未找到。");
        }

        // GetTypeByName 已经确保了它是 AbstractAiProcessorConfig 的子类
        // 但可以保留这个检查作为额外的防御层
        if (!typeof(AbstractAiProcessorConfig).IsAssignableFrom(configType))
        {
            // 这个情况理论上不会发生，因为 GetTypeByName 已经筛选过了
            return this.BadRequest($"类型 '{configTypeName}' 不是一个有效的 AI 配置类型。");
        }

        try
        {
            string schemaJson = VueFormSchemaGenerator.GenerateSchemaJson(configType);
            using var jsonNode = JsonDocument.Parse(schemaJson);
            return this.Ok(jsonNode);
        }
        catch (JsonException jsonEx)
        {
            // Log the error: schemaJson
            // Log the exception: jsonEx
            return this.StatusCode(StatusCodes.Status500InternalServerError, $"生成的 Schema 不是有效的 JSON 格式。错误: {jsonEx.Message}");
        }
        catch (Exception ex)
        {
            // Log the exception: ex
            return this.StatusCode(StatusCodes.Status500InternalServerError, $"为类型 '{configTypeName}' 生成 Schema 时发生内部错误: {ex.Message}");
        }
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
}