// namespace YAESandBox.Workflow.Runtime.RuntimePersistence;
//
// public record WorkflowInstanceRecord
// {
//     public Guid InstanceId { get; init; } // 全局唯一的工作流执行ID
//     public string WorkflowName { get; init; } // 用于识别的名称，来自Config
//     public WorkflowStatus Status { get; init; }
//     public DateTime CreatedAt { get; init; }
//
//     public DateTime? FinishedAt { get; init; }
//
//     // 可以存储工作流级别的输入参数
//     public string? SerializedInputs { get; init; }
// }
//
// public record TuumInstanceRecord
// {
//     public Guid InstanceId { get; init; } // Tuum实例的唯一ID
//     public Guid WorkflowInstanceId { get; init; } // 所属工作流实例的ID
//     public Guid? ParentInstanceId { get; init; } // 父实例ID (例如，创建它的TuumRune实例)
//
//     public string ConfigId { get; init; } // 使用的蓝图ConfigID
//     public string Name { get; init; } // 蓝图中的名称
//
//     public TuumStatus Status { get; init; }
//
//     // 使用JSON字符串来存储，因为类型是动态的
//     public string? SerializedInputs { get; private set; }
//     public string? SerializedOutputs { get; private set; }
//
//     // 用于存储当Tuum被暂停时的内部状态快照
//     public string? SerializedSuspendedState { get; private set; }
// }
//
// public enum WorkflowStatus
// {
//     Running,
//     Completed,
//     Failed
// }
//
// public enum TuumStatus
// {
//     Running,
//     Completed,
//     Failed
// }
//
// public enum RuneStatus
// {
//     Running,
//     Completed,
//     Failed
// }