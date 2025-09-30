using System.Collections.Concurrent;
using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.Runtime.RuntimePersistence.Storage;

/// <summary>
/// IPersistenceStorage 的一个纯内存实现，用于单元测试和默认的非持久化运行。
/// 使用 ConcurrentDictionary 保证线程安全。
/// </summary>
public sealed class InMemoryPersistenceStorage : IPersistenceStorage
{
    private ConcurrentDictionary<Guid, InstanceStateRecord> States { get; } = new();

    /// <inheritdoc />
    public Task<Result<InstanceStateRecord?>> GetStateAsync(Guid instanceId)
    {
        this.States.TryGetValue(instanceId, out var record);
        return Task.FromResult(Result.Ok(record));
    }

    /// <inheritdoc />
    public Task<Result> SetStateAsync(InstanceStateRecord stateRecord)
    {
        this.States[stateRecord.InstanceId] = stateRecord;
        return Task.FromResult(Result.Ok());
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyDictionary<Guid, InstanceStateRecord>>> GetAllStatesAsync()
    {
        // 返回一个字典的快照，以防止外部修改
        IReadOnlyDictionary<Guid, InstanceStateRecord> snapshot = new Dictionary<Guid, InstanceStateRecord>(this.States);
        return Task.FromResult(Result.Ok(snapshot));
    }
}