using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Depend.Schema;

/// <summary>
/// 具有 Schema 的数据对象
/// </summary>
/// <typeparam name="T"></typeparam>
public record DataWithSchemaDto<T> where T : notnull
{
    private DataWithSchemaDto() { }

    /// <summary>
    /// 实际的数据对象
    /// </summary>
    [Required]
    public required T Data { get; init; }

    /// <summary>
    /// 对应 T 类型的 JSON Schema 配置信息
    /// </summary>
    public object? Schema { get; init; }

    /// <summary>
    /// 根据data的类型生成对应的 JSON Schema
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static Result<DataWithSchemaDto<T>> Create(T data)
    {
        var moduleType = data.GetType();
        object? schemaObject;

        // 注意：我们应该缓存 Schema 生成的结果，而不是每次都重新生成
        // 这里简单调用，实际应用中应引入缓存（如 IMemoryCache）
        // 你可能需要一个 SchemaService 来封装 Schema 的生成和缓存逻辑
        string schemaJson = VueFormSchemaGenerator.GenerateSchemaJson(moduleType);
        try
        {
            // 返回 JsonNode 以确保是有效的 JSON 对象
            schemaObject = JsonNode.Parse(schemaJson);
        }
        catch (JsonException jsonEx)
        {
            return JsonError.Error(schemaJson, $"为类型 {moduleType}生成的 Schema 不是有效的 JSON 格式。错误: {jsonEx.Message}");
        }
        catch (Exception ex)
        {
            return JsonError.Error(schemaJson, $"为类型 '{moduleType}' 生成配置模板时发生内部错误: {ex.Message}");
        }

        // (可选，但推荐) 填充基于 Schema 'default' 关键字的默认值
        // 这部分逻辑可能比较复杂，因为它需要解析 Schema 并应用 default 值到 initialData 对象。
        // NJsonSchema 本身在生成 Schema 时会包含 default，但应用它到对象上需要额外代码。
        // 如果你的表单库 (如 @lljj/vue3-form-naive) 能很好地处理空对象 + Schema default，
        // 那么后端这里的 initialData 只需要包含那些 *不能* 通过 Schema default 表达的值（如外部引入的）。
        //
        // 为了简化，我们暂时假设前端表单库会处理 Schema default。
        // 如果需要后端填充，你可能需要这样的逻辑：
        // string tempSchemaForDefaults = VueFormSchemaGenerator.GenerateSchemaJson(configType);
        // initialData = ApplySchemaDefaults(initialData, tempSchemaForDefaults); // 你需要实现 ApplySchemaDefaults}

        return new DataWithSchemaDto<T>
        {
            Data = data,
            Schema = schemaObject
        };
    }
}