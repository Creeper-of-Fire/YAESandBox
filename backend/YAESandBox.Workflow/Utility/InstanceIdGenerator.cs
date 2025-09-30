using UUIDNext;

namespace YAESandBox.Workflow.Utility;

/// <summary>
/// 一个实例ID生成器，用于生成唯一的、可预测的、可追踪的实例ID。
/// </summary>
public static class InstanceIdGenerator
{
    /// <summary>
    /// 为所有确定性GUID生成定义一个固定的命名空间。
    /// 这是一个标准的UUIDv5实践，确保生成的ID不会与其他来源的GUID冲突。
    /// 这个GUID是随机生成的，并且应该在代码库中保持不变。
    /// </summary>
    private static Guid DeterministicNamespace { get; } = new("77202331-05e9-4e80-81fa-b1eb417d915b");

    /// <summary>
    /// 基于一个业务标识符，创建一个确定性的工作流实例ID (UUIDv5)。
    /// 适用于需要幂等性的场景。
    /// </summary>
    /// <param name="businessKey">一个能唯一标识这次执行的业务字符串，例如聊天记录号、用户名等。</param>
    public static Guid CreateDeterministicWorkflow(string businessKey)
    {
        // 在这里，我们使用全局的、固定的命名空间作为所有确定性工作流的“根”。
        return Uuid.NewNameBased(DeterministicNamespace, businessKey);
    }

    /// <summary>
    /// 为一次**全新**的工作流执行创建一个随机的、唯一的实例ID。
    /// <p>如果是旧工作流，会通过一个专门的API端口等，输入一个确定性的实例ID，进行“重启”。</p>
    /// </summary>
    /// <remarks>使用UUIDv7，因为它既唯一又按时间排序，非常适合作为数据库主键和日志追踪。</remarks>
    /// <returns>一个新的GUID，代表一个工作流会话。</returns>
    public static Guid CreateForNewWorkflow()
    {
        return Uuid.NewSequential();
    }

    /// <summary>
    /// 为一个子处理器创建一个确定性的 UUID Version 5 实例ID。
    /// </summary>
    /// <param name="parentInstanceId">父处理器的InstanceId，将作为子ID的命名空间。</param>
    /// <param name="childUniqueId">子处理器自身的标识ID，其应当是在父处理器的作用域下唯一的。</param>
    /// <returns>一个确定性的、符合 RFC 4122 规范的 UUID Version 5。</returns>
    public static Guid CreateForChild(Guid parentInstanceId, string childUniqueId)
    {
        return Uuid.NewNameBased(parentInstanceId, childUniqueId);
    }
}