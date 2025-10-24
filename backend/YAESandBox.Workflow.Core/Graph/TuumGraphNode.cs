using YAESandBox.Workflow.Core.Config;

namespace YAESandBox.Workflow.Core.Graph;

/// <summary>
/// 一个Tuum在Workflow图中的最终、不可变表示 (DTO)。
/// 此对象直接从 TuumConfig 创建，作为 GraphExecutor 的输入。
/// </summary>
/// <param name="tuumConfig">原始的枢机配置。</param>
public class TuumGraphNode(TuumConfig tuumConfig) : IGraphNode<string>
{
    /// <summary>
    /// 节点的唯一ID，来自 TuumConfig.ConfigId。
    /// </summary>
    public string Id { get; } = tuumConfig.ConfigId;

    /// <summary>
    /// 对原始Tuum配置的引用，用于执行。
    /// </summary>
    public TuumConfig Config { get; } = tuumConfig;

    // 获取输入端口：
    // Tuum 的输入端口是其 InputMappings 中所有“外部端点名”(Value)的去重集合。
    // 这代表了 Tuum 从外部世界消费的所有数据。
    private IEnumerable<string> InputPortNames { get; } = tuumConfig.InputMappings.Values.Distinct().ToList();

    // 获取输出端口：
    // Tuum 的输出端口是其 OutputMappings 中所有“外部端点名”的去重集合。
    // (在 TuumConfig 的设计中，一个外部输出端点名只能出现一次，
    //  但为了稳健性，我们还是使用 Distinct)
    private IEnumerable<string> OutputPortNames { get; } = tuumConfig.OutputMappings.Values
        .SelectMany(endpointSet => endpointSet)
        .Distinct()
        .ToList();


    /// <inheritdoc />
    public IEnumerable<string> GetInputPortNames()
    {
        return this.InputPortNames;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetOutputPortNames()
    {
        return this.OutputPortNames;
    }
}