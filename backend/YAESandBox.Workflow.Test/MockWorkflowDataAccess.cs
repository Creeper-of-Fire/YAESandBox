using YAESandBox.Workflow.WorkflowService.Abstractions;

namespace YAESandBox.Workflow.Test;

/// <summary>
/// IWorkflowDataAccess 的模拟实现，用于控制台环境。
/// 由于没有连接到真实的游戏状态，它只返回默认或空值。
/// </summary>
public class MockWorkflowDataAccess : IWorkflowDataAccess;