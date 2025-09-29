using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Graph;
using YAESandBox.Workflow.Rune;

namespace YAESandBox.Workflow.Tuum;

//tuum的信息：
// 使用的脚本符文们的UUID（注意，脚本符文本身就是绑定在枢机上的，如果需要把符文复制到更广的地方，可以考虑直接复制枢机之类的）
/// <summary>
/// 枢机配置的运行时
/// </summary>
public class TuumProcessor(
    WorkflowRuntimeService workflowRuntimeService,
    TuumConfig config)
    : IProcessorWithDebugDto<ITuumProcessorDebugDto>
{
    private TuumConfig Config { get; } = config;

    /// <summary>
    /// 枢机的上下文/内部运行时
    /// </summary>
    public TuumProcessorContent TuumContent { get; } = new(config, workflowRuntimeService);

    /// <summary>
    /// 枢机运行时的上下文
    /// </summary>
    public class TuumProcessorContent(TuumConfig tuumConfig, WorkflowRuntimeService workflowRuntimeService)
    {
        /// <summary>
        /// 枢机的内部变量池
        /// </summary>
        public Dictionary<string, object?> TuumVariable { get; } = [];

        /// <summary>
        /// 得到枢机的变量
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public object? GetTuumVar(string name)
        {
            return this.TuumVariable.GetValueOrDefault(name);
        }

        /// <summary>
        /// 设置枢机的变量
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetTuumVar(string name, object? value)
        {
            this.TuumVariable[name] = value;
        }

        /// <summary>
        /// 枢机的配置
        /// </summary>
        public TuumConfig TuumConfig { get; } = tuumConfig;

        /// <summary>
        /// 工作流的运行时服务
        /// </summary>
        public WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

        /// <summary>
        /// 获得枢机的变量，带有类型转换，并且有序列化尝试
        /// </summary>
        public T? GetTuumVar<T>(string valueName)
        {
            if (this.TryGetTuumVar<T>(valueName, out var value))
            {
                return value;
            }

            return default;
        }

        /// <summary>
        /// 尝试获得枢机的变量，带有类型转换，并且有序列化尝试。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="valueName">变量名。</param>
        /// <param name="value">如果成功，则输出转换后的值；否则为 null 或 default。</param>
        /// <returns>如果成功找到并转换了变量，则返回 true；否则返回 false。</returns>
        public bool TryGetTuumVar<T>(string valueName, [MaybeNullWhen(false)] out T value)
        {
            object? rawValue = this.GetTuumVar(valueName);

            // Case 1: 变量不存在或其值就是 null。
            if (rawValue is null)
            {
                value = default;
                return false;
            }

            // Case 2: 变量已经是正确的类型（最快路径）。
            if (rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            try
            {
                string json;
                if (rawValue is string stringTryGetValue)
                    json = stringTryGetValue;
                else
                    // 将 C# 对象序列化成 JSON 字符串
                    json = JsonSerializer.Serialize(rawValue);

                var result = JsonSerializer.Deserialize<T>(json, YaeSandBoxJsonHelper.JsonSerializerOptions);

                if (result is null || result.Equals(default(T)))
                {
                    value = result;
                    return false;
                }

                // 为了后续访问效率，可以将转换后的结果写回 TuumVariable
                this.SetTuumVar(valueName, result);
                value = result;
                return true;
            }
            catch (Exception ex)
            {
                value = default;
                throw new InvalidCastException(
                    $"无法将值'{valueName}'(类型: {rawValue.GetType().FullName})转换为 {typeof(T).FullName}：JSON 转换失败。", ex);
            }
        }
    }

    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

    /// <summary>
    /// 此枢机声明的所有输入端点的名称。
    /// </summary>
    internal IEnumerable<string> InputEndpoints { get; } = config.InputMappings.Values;

    /// <summary>
    /// 此枢机声明的所有输出端点的名称。
    /// </summary>
    internal IEnumerable<string> OutputEndpoints { get; } = config.OutputMappings.Values.SelectMany(v => v).Distinct();

    private List<IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto>> Runes { get; } =
        config.Runes.Select(rune => rune.ToRuneProcessor(workflowRuntimeService)).ToList();

    // 缓存所有Rune的运行时实例，以ConfigId为键
    private Dictionary<string, IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto>> RuneProcessors { get; } =
        config.Runes.ToDictionary(
            runeConfig => runeConfig.ConfigId,
            runeConfig => runeConfig.ToRuneProcessor(workflowRuntimeService)
        );

    public async Task<Result<Dictionary<string, object?>>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken = default)
    {
        // --- 1. 预处理阶段：构建完整的Rune图 ---

        // a. 创建虚拟输入/输出节点实例
        var virtualInputNode = new VirtualInputRune(this.Config, inputs);
        var virtualOutputNode = new VirtualOutputRune(this.Config);

        // b. 将真实Rune转换为RuneGraphNode
        var enabledRunes = this.Config.Runes.Where(r => r.Enabled).ToList();
        var realRuneNodes = RuneGraphNodeBuilder.BuildRuneGraphNodes(enabledRunes);

        // c. 组合成完整的节点列表
        var allGraphNodes = new List<IGraphNode<string>> { virtualInputNode };
        allGraphNodes.AddRange(realRuneNodes);
        allGraphNodes.Add(virtualOutputNode);

        // --- 2. 确定连接关系 ---
        List<GraphConnection<string>> connections;
        if (this.Config.Graph?.Connections is null or { Count: 0 } || this.Config.Graph.EnableAutoConnect)
        {
            // 自动连接：注意，自动连接的输入是 allGraphNodes，这样虚拟节点也能参与连接
            var autoConnectResult = GraphExecutor.TryAutoConnect<IGraphNode<string>, string>(allGraphNodes);
            if (autoConnectResult.TryGetError(out var autoConnectError, out var autoConnectValue))
                return autoConnectError;
            connections = autoConnectValue;
        }
        else
        {
            // 手动连接：需要将 TuumConfig.Connections 转换为 GraphConnection<string>
            connections = this.Config.Graph.Connections.Select(c =>
                new GraphConnection<string>(
                    new GraphConnectionEndpoint<string>(c.Source.RuneConfigId, c.Source.PortName),
                    new GraphConnectionEndpoint<string>(c.Target.RuneConfigId, c.Target.PortName)
                )).ToList();
        }

        // --- 3. 执行阶段：调用通用执行器 ---

        // a. 准备初始数据：只有虚拟输入节点会产生初始数据
        var initialData = new Dictionary<GraphConnectionEndpoint<string>, object?>();
        var initialOutputs = virtualInputNode.ProduceOutputs();
        foreach (var (portName, value) in initialOutputs)
        {
            initialData[new GraphConnectionEndpoint<string>(VirtualInputRune.VIRTUAL_INPUT_ID, portName)] = value;
        }

        // b. 定义节点执行委托
        var runeProcessorsCache = new ConcurrentDictionary<string, IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto>>();

        // c. 调用执行器
        var executionResult = await GraphExecutor.ExecuteAsync(
            realRuneNodes, // 我们只要求执行器执行真实的Rune
            connections,
            initialData,
            ExecuteNodeAsync,
            null,
            cancellationToken);

        if (executionResult.TryGetError(out var execError, out var finalDataStore))
        {
            return Result.Fail($"Tuum '{this.Config.ConfigId}' 图执行失败。", execError);
        }

        // --- 4. 后处理阶段：收集结果 ---

        // a. 提取流向虚拟输出节点的数据
        var inputsForOutputNode = new Dictionary<string, object?>();
        var connectionsToOutputNode = connections.Where(c => c.Target.NodeId == VirtualOutputRune.VIRTUAL_OUTPUT_ID);
        foreach (var conn in connectionsToOutputNode)
        {
            if (finalDataStore.TryGetValue(conn.Source, out var value))
            {
                inputsForOutputNode[conn.Target.PortName] = value;
            }
        }

        // b. 调用虚拟输出节点的收集方法
        var tuumOutputs = virtualOutputNode.CollectInputs(inputsForOutputNode);

        // TODO: 更新DebugDto

        return Result.Ok(tuumOutputs);

        async Task<Result<Dictionary<string, object?>>> ExecuteNodeAsync(
            IGraphNode<string> node, IReadOnlyDictionary<string, object?> nodeInputs, CancellationToken ct)
        {
            if (node is not RuneGraphNode runeNode)
            {
                // 虚拟节点在此阶段不执行任何操作
                return new Dictionary<string, object?>();
            }

            var processor = runeProcessorsCache.GetOrAdd(
                runeNode.Id,
                _ => runeNode.Config.ToRuneProcessor(this.WorkflowRuntimeService)
            );

            // --- 适配器 ---

            // 1. 创建一个临时的、为本次执行定制的 TuumProcessorContent
            var transientTuumContent = new TuumProcessorContent(
                this.Config, // 父Tuum的配置
                this.WorkflowRuntimeService
            );

            // 2. 将图执行器提供的输入，填充到临时 TuumVariable 池中
            foreach (var (portName, value) in nodeInputs)
            {
                transientTuumContent.SetTuumVar(portName, value);
            }

            // 3. 调用 Rune 原本的 ExecuteAsync 方法
            // (仅处理INormalRune，未来可扩展)
            if (processor is INormalRune<AbstractRuneConfig, IRuneProcessorDebugDto> normalRune)
            {
                var runeResult = await normalRune.ExecuteAsync(transientTuumContent, ct);
                // 4. 检查 Rune 执行是否失败
                if (runeResult.TryGetError(out var runeError))
                {
                    return runeError;
                }
            }

            // 5. 从临时的 TuumVariable 池中，提取出 Rune 的输出
            var outputs = new Dictionary<string, object?>();
            // 我们根据 Rune 的声明 (ProducedSpec) 来精确提取输出，而不是猜测
            foreach (var producedSpec in runeNode.Config.GetProducedSpec())
            {
                // GetTuumVar 会返回 null 如果 key 不存在，这符合预期
                var outputValue = transientTuumContent.GetTuumVar(producedSpec.Name);
                outputs[producedSpec.Name] = outputValue;
            }

            // 6. 将提取出的输出返回给图执行器
            return outputs;
        }
    }


    /// <inheritdoc />
    public ITuumProcessorDebugDto DebugDto => new TuumProcessorDebugDto
        { RuneProcessorDebugDtos = this.Runes.ConvertAll(it => it.DebugDto) };

    /// <inheritdoc />
    internal record TuumProcessorDebugDto : ITuumProcessorDebugDto
    {
        /// <inheritdoc />
        public required IList<IRuneProcessorDebugDto> RuneProcessorDebugDtos { get; init; }
    }
}