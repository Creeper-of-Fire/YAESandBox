using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace YAESandBox.Depend.Schema;

/// <summary>
/// 定义一个用于转换由 .NET 9 JsonSchemaExporter 生成的 Schema 节点的处理器。
/// 这是适配新版 Exporter 的处理器接口。
/// </summary>
public interface IYaeSchemaProcessor
{
    /// <summary>
    /// 处理一个给定的 Schema 节点。
    /// </summary>
    /// <param name="context">由 JsonSchemaExporter 提供的上下文，包含类型和属性信息。</param>
    /// <param name="schema">当前正在处理的 JsonNode 模式。处理器可以就地修改此节点。</param>
    void Process(JsonSchemaExporterContext context, JsonObject schema);
}