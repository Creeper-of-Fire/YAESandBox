using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nito.Disposables.Internals;
using YAESandBox.Authentication;
using YAESandBox.Depend.Schema;
using YAESandBox.Workflow.AIService.AiConfig;

namespace YAESandBox.Workflow.AIService.API.Controller;

/// <summary>
/// 提供与 AI 配置相关的 Schema 和类型定义信息。
/// 主要用于支持前端动态构建配置表单。
/// </summary>
[ApiExplorerSettings(GroupName = AiServiceConfigModule.AiConfigGroupName)]
[ApiController]
[Route("api/ai-configuration-management")]
public class AiConfigSchemasController : AuthenticatedApiControllerBase
{
    /// <summary>
    /// 获取所有可用的 AI 配置【类型定义及对应的UI Schema】。
    /// 此端点一次性返回所有信息，用于前端高效构建配置选择列表和动态表单，取代了之前先获取类型列表再逐个请求 Schema 的流程。
    /// </summary>
    /// <returns>一个列表，每个对象包含：
    /// 'Value': 配置类型的编程名称 (如 "DoubaoAiProcessorConfig")。
    /// 'Label': 用户友好的类型显示名称 (如 "豆包AI模型")。
    /// 'Schema': 该类型的完整 JSON Schema (包含 ui: 指令)，用于动态生成表单。
    /// </returns>
    /// <response code="200">成功获取所有 AI 配置类型的定义及其 Schema。</response>
    /// <response code="500">生成 Schema 过程中发生内部服务器错误。</response>
    [HttpGet("definitions")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<AiConfigTypeWithSchemaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<List<AiConfigTypeWithSchemaDto>> GetAllConfigTypesWithSchemas()
    {
        try
        {
            var availableTypes = ConfigSchemasHelper.GetAvailableAiConfigConcreteTypes();

            var result = availableTypes.Select(type =>
                {
                    // 1. 获取用户友好的显示名称 (Label)
                    var displayAttr = type.GetCustomAttribute<DisplayAttribute>(false);
                    string label = displayAttr?.GetName() ?? type.Name;

                    if (label.EndsWith("AiProcessorConfig", StringComparison.OrdinalIgnoreCase))
                    {
                        label = label[..^"AiProcessorConfig".Length].Trim();
                    }
                    else if (label.EndsWith("Config", StringComparison.OrdinalIgnoreCase))
                    {
                        label = label[..^"Config".Length].Trim();
                    }

                    if (string.IsNullOrEmpty(label))
                    {
                        label = type.Name; // Fallback
                    }

                    // 2. 生成并解析该类型的 JSON Schema
                    string schemaJson = YaeSchemaExporter.GenerateSchemaJson(type);
                    var schemaNode = JsonNode.Parse(schemaJson); // 解析为 JsonNode 以便正确序列化为 JSON 对象

                    // 3. 组合成 DTO
                    if (schemaNode == null)
                        return null;

                    return new AiConfigTypeWithSchemaDto
                    {
                        Value = type.Name,
                        Label = label,
                        Schema = schemaNode
                    };
                })
                .WhereNotNull()
                .OrderBy(dto => dto.Label)
                .ToList();

            return this.Ok(result);
        }
        catch (Exception ex)
        {
            // 任何一个类型的 Schema 生成失败，都会导致整个请求失败，并记录详细错误
            // Log the exception: ex
            return this.StatusCode(StatusCodes.Status500InternalServerError, $"生成 AI 配置类型定义列表时发生内部错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 代表一个包含完整定义的 AI 配置类型，用于前端一次性加载。
    /// </summary>
    public class AiConfigTypeWithSchemaDto
    {
        /// <summary>
        /// 选项的实际值，即配置类型的编程名称 (例如 "DoubaoAiProcessorConfig")。
        /// </summary>
        [Required]
        public string Value { get; init; } = string.Empty;

        /// <summary>
        /// 选项在UI上显示的文本 (例如 "豆包AI模型")。
        /// </summary>
        [Required]
        public string Label { get; init; } = string.Empty;

        /// <summary>
        /// 用于动态生成表单的 JSON Schema (包含UI指令)。
        /// </summary>
        [Required]
        public object Schema { get; init; } = new JsonObject();
    }
}