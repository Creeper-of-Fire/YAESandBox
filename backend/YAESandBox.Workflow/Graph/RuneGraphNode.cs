using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.Graph;

internal static class RuneGraphNodeBuilder
{
    /// <summary>
    /// **分析函数：构建Rune图节点**
    /// 遍历Tuum中的有序Rune列表，生成最终的图节点DTO。
    /// 此方法会根据数据流上下文，智能判断可选端口是否变为实际必需。
    /// </summary>
    /// <param name="runesInOrder">按执行顺序排列的已启用Rune列表。</param>
    /// <returns>一个包含所有Rune的图节点表示的列表。</returns>
    public static List<RuneGraphNode> BuildRuneGraphNodes(IReadOnlyList<AbstractRuneConfig> runesInOrder)
    {
        var graphNodes = new List<RuneGraphNode>();
        var providedPorts = new HashSet<string>();

        // 模拟线性执行流
        foreach (var runeConfig in runesInOrder)
        {
            var effectiveInputs = new HashSet<string>();

            // 1. 确定当前Rune的实际必需输入
            foreach (var spec in runeConfig.GetConsumedSpec())
            {
                // 条件1: 端口是静态声明必需的
                if (!spec.IsOptional)
                {
                    effectiveInputs.Add(spec.Name);
                }
                // 条件2: 端口是可选的，但在当前时间点，上游已经提供了数据
                // 这种情况下，为了数据流的完整性，这个可选端口也必须被连接
                else if (providedPorts.Contains(spec.Name))
                {
                    effectiveInputs.Add(spec.Name);
                }
            }

            // 2. 创建并添加DTO
            var allOutputs = runeConfig.GetProducedSpec().Select(s => s.Name);
            var graphNode = new RuneGraphNode(runeConfig, effectiveInputs, allOutputs);
            graphNodes.Add(graphNode);

            // 3. 更新已提供端口的集合，供后续Rune使用
            foreach (var portName in graphNode.GetOutputPortNames())
            {
                providedPorts.Add(portName);
            }
        }

        return graphNodes;
    }
}

/// <summary>
/// 一个Rune在Tuum图中的最终、不可变表示 (DTO)。
/// 此对象在Tuum的预处理阶段创建和填充。
/// </summary>
public class RuneGraphNode(AbstractRuneConfig config, IEnumerable<string> effectiveInputPorts, IEnumerable<string> allOutputPorts)
    : IGraphNode<string>
{
    /// <summary>
    /// 节点的唯一ID，来自 RuneConfig.ConfigId。
    /// </summary>
    public string Id { get; } = config.ConfigId;

    /// <summary>
    /// 对原始Rune配置的引用，用于执行。
    /// </summary>
    public AbstractRuneConfig Config { get; } = config;

    // 这两个集合由 TuumAnalysisService 填充
    private HashSet<string> EffectiveInputPorts { get; } = [..effectiveInputPorts];
    private HashSet<string> AllOutputPorts { get; } = [..allOutputPorts];

    /// <inheritdoc />
    public IEnumerable<string> GetInputPortNames()
    {
        // 直接返回分析器计算出的、实际必需的输入端口
        return this.EffectiveInputPorts;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetOutputPortNames()
    {
        // 返回所有可能的输出端口
        return this.AllOutputPorts;
    }
}