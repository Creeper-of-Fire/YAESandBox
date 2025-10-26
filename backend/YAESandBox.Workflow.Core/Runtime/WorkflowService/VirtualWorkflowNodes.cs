using YAESandBox.Workflow.Core.Config;
using YAESandBox.Workflow.Core.Graph;

namespace YAESandBox.Workflow.Core.Runtime.WorkflowService;

/// <summary>
/// 一个虚拟的、仅存在于运行时的图节点，代表Workflow的入口。
/// 它的作用是将工作流的启动参数注入到Tuum图中。
/// </summary>
public class VirtualWorkflowInputNode(WorkflowConfig config, IReadOnlyDictionary<string, object?> workflowInitialInputs) 
    : IGraphNode<string>
{
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// 虚拟输入端口的ID
    /// </summary>
    public const string VIRTUAL_INPUT_ID = "__workflow_input__";
        
    private IReadOnlyList<string> WorkflowInputNames { get; } = config.WorkflowInputs;
    private IReadOnlyDictionary<string, object?> WorkflowInitialInputs { get; } = workflowInitialInputs;

    /// <summary>
    /// 本虚拟节点的ID是固定的，是VIRTUAL_INPUT_ID
    /// </summary>
    public string Id => VIRTUAL_INPUT_ID;

    // 工作流输入节点不消费任何东西
    /// <summary>
    /// 获得工作流输入节点的输入端口名
    /// </summary>
    public IEnumerable<string> GetInputPortNames() => [];

    // 它的输出端口就是WorkflowConfig中声明的所有入口参数名
    /// <summary>
    /// 获得工作流输入节点的输出端口名
    /// </summary>
    public IEnumerable<string> GetOutputPortNames() => this.WorkflowInputNames;

    /// <summary>
    /// "执行"这个虚拟节点，产生工作流的初始数据。
    /// </summary>
    /// <returns>一个字典，Key是入口参数名，Value是对应的数据。</returns>
    public Dictionary<string, object?> ProduceOutputs()
    {
        var outputs = new Dictionary<string, object?>();
        foreach (string inputName in this.WorkflowInputNames)
        {
            this.WorkflowInitialInputs.TryGetValue(inputName, out object? value);
            outputs[inputName] = value;
        }
        return outputs;
    }
}