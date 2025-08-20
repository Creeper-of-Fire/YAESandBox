using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace YAESandBox.Depend.Schema;

/// <summary>
/// 为 YaeSchemaExporter 提供配置选项。
/// </summary>
public class YaeSchemaOptions
{
    /// <summary>
    /// 获取处理器列表，允许外部添加、移除或重排处理器。
    /// </summary>
    public List<IYaeSchemaProcessor> SchemaProcessors { get; } = [];

    /// <summary>
    /// 一个委托，允许在所有内置处理器运行【之后】，
    /// 对最终生成的 Schema 节点进行自定义的后处理。
    /// </summary>
    public Action<JsonNode>? PostProcessSchema { get; set; }

    /// <summary>
    /// 一个委托，允许在所有内置处理器运行【之前】，
    /// 对 Schema 节点进行自定义的预处理。
    /// </summary>
    public Action<JsonSchemaExporterContext, JsonObject>? PreProcessNode { get; set; }
}