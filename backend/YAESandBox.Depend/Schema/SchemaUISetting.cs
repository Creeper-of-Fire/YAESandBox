using System.Text.Json.Serialization;
using NJsonSchema;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Depend.Schema;

/// 似乎存在什么bug，导致Json序列化时，属性直接使用的属性名称，而没有使用JsonPropertyName特性，所以这里把属性都小写了
public record SchemaUiSetting
{
    /// <inheritdoc cref="StringOptionsAttribute.OptionsProviderEndpoint"/>
    [JsonPropertyName("optionsProviderEndpoint")]
    public string? OptionsProviderEndpoint { get; set; }

    /// <inheritdoc cref="StringOptionsAttribute.IsEditableSelectOptions"/>
    [JsonPropertyName("isEditableSelectOptions")]
    public bool? IsEditableSelectOptions { get; set; } = false;

    /// <summary>
    /// TODO 未在前端实现
    /// 
    /// 把属性值渲染为带模态框的按钮
    /// </summary>
    [JsonPropertyName("toButtonPopUp")] public bool? ToButtonPopUp { get; set; }

    /// <summary>
    /// 扩展内容
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object?> ExtensionData { get; set; } = [];
}

internal static class SchemaUiSettingHelper
{
    /// <summary>
    /// 安全地获取或创建属性 Schema 的 ui:options 扩展数据字典。
    /// </summary>
    /// <param name="schema">属性的 JsonSchema 对象。</param>
    /// <returns>一个<see cref="SchemaUiSetting"/>，用于存储 ui:options。</returns>
    internal static SchemaUiSetting GetOrCreateUiOptions(this JsonSchema schema)
    {
        schema.ExtensionData ??= new Dictionary<string, object?>();

        if (schema.ExtensionData.TryGetValue(UiOptionsKey, out object? opt) && opt is SchemaUiSetting existingUiOptions)
            return existingUiOptions;

        var newUiOptions = new SchemaUiSetting();
        schema.ExtensionData[UiOptionsKey] = newUiOptions;
        return newUiOptions;
    }

    private const string UiOptionsKey = "ui:options";

    /// <summary>
    /// 重新把SchemaUISetting处理为普通格式
    /// </summary>
    public static void ProcessUiOption(this JsonSchema schema)
    {
        schema.ExtensionData ??= new Dictionary<string, object?>();
        if (schema.ExtensionData.TryGetValue(UiOptionsKey, out object? opt) && opt is SchemaUiSetting existingUiOptions)
        {
            schema.ExtensionData[UiOptionsKey] = YaeSandBoxJsonHelper.ToDictionaryWithJsonPropertyNames(existingUiOptions);
        }
    }
}