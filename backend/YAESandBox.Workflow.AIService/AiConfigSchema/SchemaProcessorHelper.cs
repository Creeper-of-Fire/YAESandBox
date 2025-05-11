using NJsonSchema;

namespace YAESandBox.Workflow.AIService.AiConfigSchema;

/// <summary>
/// Schema Processor 的辅助方法。
/// </summary>
public static class SchemaProcessorHelper
{
    /// <summary>
    /// 安全地获取或创建属性 Schema 的 ui:options 扩展数据字典。
    /// </summary>
    /// <param name="schema">属性的 JsonSchema 对象。</param>
    /// <returns>一个可修改的字典，用于存储 ui:options。</returns>
    public static IDictionary<string, object> GetOrCreateUiOptions(JsonSchema schema)
    {
        const string uiOptionsKey = "ui:options";
        schema.ExtensionData ??= new Dictionary<string, object?>();

        if (schema.ExtensionData.TryGetValue(uiOptionsKey, out object? opt) && opt is IDictionary<string, object> existingUiOptions)
            return existingUiOptions;

        var newUiOptions = new Dictionary<string, object>();
        schema.ExtensionData[uiOptionsKey] = newUiOptions;
        return newUiOptions;
    }
}