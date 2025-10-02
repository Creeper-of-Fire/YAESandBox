using YAESandBox.Workflow.Runtime.Processor;

namespace YAESandBox.Workflow.Runtime.InstanceId;

/// <summary>
/// 负责在一个父处理器的作用域内，为其子处理器生成唯一的、确定性的实例ID。
/// </summary>
public static class ExecutionScopeManager
{
    /// <summary>
    /// 根据父处理器，为一个子处理器创建并准备好它的运行时上下文。
    /// 这个方法是幂等的：对于相同的childUniqueId，它将返回相同的上下文。
    /// </summary>
    /// <param name="parentContext">父处理器的上下文。</param>
    /// <param name="childUniqueId">子处理器自身的标识ID，其应当是在父处理器的作用域下唯一的。</param>
    /// <returns>一个为子处理器准备好的CreatingContext。</returns>
    public static ICreatingContext CreateChildWithScope(this ProcessorContext parentContext, string childUniqueId)
    {
        var childInstanceId = InstanceIdGenerator.CreateForChild(
            parentContext.InstanceId,
            childUniqueId
        );

        return parentContext.CreateContextForChild(childInstanceId);
    }
}