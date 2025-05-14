using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Depend.Schema;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.AIService.AiConfig;

namespace YAESandBox.Workflow.AIService.Controller;

[ApiExplorerSettings(GroupName = AiConfigurationsController.AiConfigGroupName)]
[ApiController]
[Route("api/ai-configuration-management")] // 更改路由以反映其更广泛的职责（如果合并了实例列表）或保持 schema 特定
[Obsolete("现在应当从其他地方获取表单结构")]
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

        Type? configType = ConfigSchemasHelper.GetTypeByName(configTypeName);

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

            // 为了确保返回的是一个有效的 JSON 对象而不是字符串，我们可以解析它
            // 这也有助于捕获生成阶段可能出现的序列化问题
            try
            {
                var jsonDocument = JsonDocument.Parse(schemaJson);
                return this.Ok(jsonDocument.RootElement.Clone()); // 返回 JsonElement，ASP.NET Core 会正确序列化它
            }
            catch (JsonException jsonEx)
            {
                // Log the error: schemaJson
                // Log the exception: jsonEx
                return this.StatusCode(StatusCodes.Status500InternalServerError, $"生成的 Schema 不是有效的 JSON 格式。错误: {jsonEx.Message}");
            }
        }
        catch (Exception ex)
        {
            // Log the exception: ex
            return this.StatusCode(StatusCodes.Status500InternalServerError, $"为类型 '{configTypeName}' 生成 Schema 时发生内部错误: {ex.Message}");
        }
    }
}