using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NJsonSchema;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Depend.Schema;

/// 似乎存在什么bug，导致Json序列化时，属性直接使用的属性名称，而没有使用JsonPropertyName特性，所以这里把属性都小写了
public class SchemaUISetting
{
    /// <inheritdoc cref="StringOptionsAttribute.OptionsProviderEndpoint"/>
    [JsonPropertyName("optionsProviderEndpoint")]
    public string? optionsProviderEndpoint { get; set; }

    /// <inheritdoc cref="StringOptionsAttribute.IsEditableSelectOptions"/>
    [JsonPropertyName("isEditableSelectOptions")]
    public bool? isEditableSelectOptions { get; set; } = false;

    [JsonPropertyName("toButtonPopUp")] public bool? toButtonPopUp { get; set; }

    /// <summary>
    /// 安全地获取或创建属性 Schema 的 ui:options 扩展数据字典。
    /// </summary>
    /// <param name="schema">属性的 JsonSchema 对象。</param>
    /// <returns>一个<see cref="SchemaUISetting"/>，用于存储 ui:options。</returns>
    public static SchemaUISetting GetOrCreateUiOptions(JsonSchema schema)
    {
        const string uiOptionsKey = "ui:options";
        schema.ExtensionData ??= new Dictionary<string, object?>();

        if (schema.ExtensionData.TryGetValue(uiOptionsKey, out object? opt) && opt is SchemaUISetting existingUiOptions)
            return existingUiOptions;

        var newUiOptions = new SchemaUISetting();
        schema.ExtensionData[uiOptionsKey] = newUiOptions;
        return newUiOptions;
    }

    /// <summary>
    /// 重新把SchemaUISetting处理为普通格式
    /// </summary>
    public static void ProcessUiOption(JsonSchema schema)
    {
        const string uiOptionsKey = "ui:options";
        schema.ExtensionData ??= new Dictionary<string, object?>();
        if (schema.ExtensionData.TryGetValue(uiOptionsKey, out object? opt) && opt is SchemaUISetting existingUiOptions)
        {
            schema.ExtensionData[uiOptionsKey] = existingUiOptions;
        }
    }
}