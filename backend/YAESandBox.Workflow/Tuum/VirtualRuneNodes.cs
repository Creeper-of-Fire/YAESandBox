using YAESandBox.Workflow.Graph;

namespace YAESandBox.Workflow.Tuum;

/// <summary>
/// 一个虚拟的、仅存在于运行时的符文，代表Tuum的入口。
/// 它的作用是将Tuum的输入数据注入到Rune图中。
/// </summary>
internal class VirtualInputRune(TuumConfig config, IReadOnlyDictionary<string, object?> tuumInitialInputs)
    : IGraphNode<string>
{
    // ReSharper disable once InconsistentNaming
    public const string VIRTUAL_INPUT_ID = "@tuum_input";

    // <internalName, endpointName>
    private IReadOnlyDictionary<string, string> InputMappings { get; } = config.InputMappings;
    private IReadOnlyDictionary<string, object?> TuumInitialInputs { get; } = tuumInitialInputs;

    public string Id => VIRTUAL_INPUT_ID;

    // 输入符文不消费任何东西
    public IEnumerable<string> GetInputPortNames() => Enumerable.Empty<string>();

    // 它的输出端口就是Tuum所有映射的内部变量名
    public IEnumerable<string> GetOutputPortNames() => this.InputMappings.Keys;

    /// <summary>
    /// “执行”这个虚拟节点。
    /// 它会根据Tuum的输入映射，从Tuum的总输入中提取数据，并作为自己的输出。
    /// </summary>
    /// <returns>一个字典，Key是内部变量名，Value是对应的数据。</returns>
    public Dictionary<string, object?> ProduceOutputs()
    {
        var outputs = new Dictionary<string, object?>();
        foreach ((string internalName, string endpointName) in this.InputMappings)
        {
            this.TuumInitialInputs.TryGetValue(endpointName, out object? value);
            outputs[internalName] = value;
        }

        return outputs;
    }
}

/// <summary>
/// 一个虚拟的、仅存在于运行时的符文，代表Tuum的出口。
/// 它的作用是从Rune图中收集最终结果，作为Tuum的输出。
/// </summary>
internal class VirtualOutputRune(TuumConfig config) : IGraphNode<string>
{
    // ReSharper disable once InconsistentNaming
    public const string VIRTUAL_OUTPUT_ID = "@tuum_output";

    // Key: internalName, Value: Set of endpointNames
    private IReadOnlyDictionary<string, HashSet<string>> OutputMappings { get; } = config.OutputMappings;

    public string Id => VIRTUAL_OUTPUT_ID;

    // 它的输入端口是Tuum所有映射的内部变量名
    public IEnumerable<string> GetInputPortNames() => this.OutputMappings.Keys;

    // 输出符文不生产任何东西
    public IEnumerable<string> GetOutputPortNames() => Enumerable.Empty<string>();

    /// <summary>
    /// “执行”这个虚拟节点。
    /// 它会接收输入数据，并根据Tuum的输出映射，将其整理成Tuum的最终输出格式。
    /// </summary>
    /// <param name="inputs">连接到此节点的所有输入数据，Key是内部变量名。</param>
    /// <returns>一个字典，Key是外部端点名，Value是对应的数据。</returns>
    public Dictionary<string, object?> CollectInputs(IReadOnlyDictionary<string, object?> inputs)
    {
        var tuumFinalOutputs = new Dictionary<string, object?>();
        foreach ((string internalName, var endpointNames) in this.OutputMappings)
        {
            if (!inputs.TryGetValue(internalName, out object? value)) 
                continue;

            foreach (string endpointName in endpointNames)
            {
                tuumFinalOutputs[endpointName] = value;
            }
        }

        return tuumFinalOutputs;
    }
}