namespace YAESandBox.Workflow.Core;

/// <summary>
/// 代表工作流中一个可连接的端点。
/// 它由祝祷的唯一ID和该祝祷上的一个输入/输出变量名组成。
/// </summary>
/// <param name="TuumId">端点所属祝祷的ConfigId。</param>
/// <param name="EndpointName">端点的名称，对应于TuumConfig中Input/Output Mappings的Value。</param>
public record TuumConnectionEndpoint(string TuumId, string EndpointName);

/// <summary>
/// 定义了工作流中两个祝祷端点之间的一条有向连接。
/// </summary>
/// <param name="Source">数据来源的输出端点。</param>
/// <param name="Target">数据流向的输入端点。</param>
public record WorkflowConnection(TuumConnectionEndpoint Source, TuumConnectionEndpoint Target);