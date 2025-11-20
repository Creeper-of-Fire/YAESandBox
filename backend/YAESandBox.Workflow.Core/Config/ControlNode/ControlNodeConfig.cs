using System.Text.Json.Serialization;

namespace YAESandBox.Workflow.Core.Config.ControlNode;
/// <summary>
/// 控制流节点的基类配置。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ForeachNodeConfig), "foreach")]
[JsonDerivedType(typeof(IfNodeConfig), "if")]
// [JsonDerivedType(typeof(WhileNodeConfig), "while")] // 稍后实现
public abstract record ControlNodeConfig
{
    /// <summary>
    /// 
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// 
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// 此控制节点包含的“子图”模板。
    /// 例如：循环体、If的True分支等。
    /// </summary>
    public List<TuumConfig> SubGraphTuums { get; init; } = [];
    
    // 也可以包含 SubGraphConnections，用于定义子图内部的连接
    /// <summary>
    /// 子图之间的连接。
    /// </summary>
    public List<WorkflowConnection> SubGraphConnections { get; init; } = [];
}

/// <summary>
/// Foreach 循环节点配置。
/// </summary>
public record ForeachNodeConfig : ControlNodeConfig
{
    /// <summary>
    /// 输入集合的来源映射 (e.g., "items_source" -> "some_previous_node.list_output")
    /// </summary>
    public required string CollectionInputEndpoint { get; init; }

    /// <summary>
    /// 在子图中，代表当前迭代项的变量名 (e.g., "item")。
    /// 子图中的 Tuum 可以通过 input mapping 引用这个名字来获取当前项。
    /// </summary>
    public required string LoopItemVariableName { get; init; }

    /// <summary>
    /// (可选) 需要从循环中收集并聚合输出的端点名列表。
    /// e.g., ["processed_result"] -> 输出 List&lt;Result&gt;
    /// </summary>
    public List<string> CollectOutputs { get; init; } = [];
}

/// <summary>
/// If 条件节点配置。
/// </summary>
public record IfNodeConfig : ControlNodeConfig
{
    /// <summary>
    /// 条件输入的来源 (必须是 bool 类型)。
    /// </summary>
    public required string ConditionInputEndpoint { get; init; }

    /// <summary>
    /// False 分支的子图模板 (True 分支使用基类的 SubGraphTuums)。
    /// </summary>
    public List<TuumConfig> ElseBranchTuums { get; init; } = [];
    
    /// <summary>
    /// False 分支的子图连接 (True 分支使用基类的 SubGraphConnections)。
    /// </summary>
    public List<WorkflowConnection> ElseBranchConnections { get; init; } = [];
}