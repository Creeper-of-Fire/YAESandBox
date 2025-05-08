// 需要引入

using System.Reflection;

namespace YAESandBox.Workflow.AIService.AiConfigSchema;

[ApiController]
[Route("api/config-schemas")]
public class ConfigSchemasController : ControllerBase
{
    // 假设你有一个服务来获取所有已注册的 AI 配置类型
    // private readonly IConfigTypeProvider _configTypeProvider;
    // public ConfigSchemasController(IConfigTypeProvider configTypeProvider) {
    //     _configTypeProvider = configTypeProvider;
    // }

    [HttpGet("{configTypeName}")]
    public ActionResult<List<FormFieldSchema>> GetSchema(string configTypeName)
    {
        // 实际项目中，你会有一个机制来安全地从 configTypeName 解析到 Type
        // 这里为了简单，我们硬编码一下查找
        Type? configType = ConfigSchemasBuildHelper.GetTypeByName(configTypeName);

        if (configType == null || !typeof(AiConfigBase).IsAssignableFrom(configType))
        {
            return NotFound($"Configuration type '{configTypeName}' not found or is not a valid AI config type.");
        }

        return Ok(ConfigSchemasBuildHelper.GenerateSchemaForType(configType));
    }

    [HttpGet("available-types")]
    public ActionResult<List<object>> GetAvailableConfigTypes()
    {
        // 这里应该动态获取所有 AiConfigBase 的子类
        // 为简单起见，我们硬编码
        var types = new[]
        {
            new { value = "OpenAI", label = "OpenAI 大模型" },
            new { value = "AzureOpenAI", label = "Azure OpenAI 服务" },
            // 添加其他类型
        };
        return Ok(types);
    }

    private static Type? GetTypeByName(string typeName)
    {
        // 注意：这里的查找方式非常基础，仅用于演示。
        // 在实际项目中，你可能需要扫描特定程序集或使用更安全的类型解析机制。
        // 确保只允许查找你期望的配置类型，避免安全风险。
        var assembly = Assembly.GetExecutingAssembly(); // 或者包含你的 record 定义的程序集
        return assembly.GetTypes().FirstOrDefault(t =>
            t.Name.Equals(typeName + "Config", StringComparison.OrdinalIgnoreCase) || // OpenAiConfig
            t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)); // OpenAI
    }

    // 辅助方法：根据名称获取类型 (实际项目中应更健壮)
}