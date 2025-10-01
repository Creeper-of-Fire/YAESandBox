using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;

namespace YAESandBox.Workflow.Runtime.RuntimePersistence.Storage;

/// <summary>
/// 代表一个存储在单文件中的、完整工作流执行的所有持久化状态。
/// </summary>
public record WorkflowPersistenceFileRecord
{
    /// <summary>
    /// 工作流根实例的ID，也用于标识这个文件。
    /// </summary>
    public Guid WorkflowInstanceId { get; init; }

    /// <summary>
    /// 存储此工作流执行中所有子实例（Tuum, Rune等）的状态。
    /// Key 是子实例的唯一 InstanceId。
    /// </summary>
    public Dictionary<Guid, InstanceStateRecord> InstanceStates { get; init; } = new();
}

/// <summary>
/// 一个基于【单文件】的 IPersistenceStorage 实现。
/// 它将一个完整工作流的所有实例状态聚合存储在一个 JSON 文件中。
/// </summary>
/// <param name="storageScope">
/// 已经配置好作用域的存储实例。
/// 所有持久化数据都将保存在这个作用域的根目录下。
/// 例如，一个指向 "/Saves/{UserId}/WorkflowRuns/Persistence" 的作用域。
/// </param>
/// <param name="workflowInstanceId">
/// 工作流根实例的ID。
/// </param>
public class JsonFilePersistenceStorage(ScopedJsonStorage storageScope, Guid workflowInstanceId) : IPersistenceStorage
{
    private const string PersistenceFolderPath = "WorkflowRuns";
    private const string PersistenceFolderName = "Persistence";

    /// <summary>
    /// 存储持久化记录的文件夹作用域。
    /// </summary>
    public static ScopeTemplate PersistenceScope { get; } =
        SaveRoot().CreateScope(PersistenceFolderPath).CreateScope(PersistenceFolderName);

    private ScopedJsonStorage StorageScope { get; } = storageScope;
    private const string StateFileExtension = ".json";
    private string StateFileName { get; } = $"{workflowInstanceId}{StateFileExtension}";

    /// <summary>
    /// 从文件加载完整的持久化记录。
    /// </summary>
    private async Task<Result<WorkflowPersistenceFileRecord>> LoadRecordAsync()
    {
        var loadResult = await this.StorageScope.LoadAllAsync<WorkflowPersistenceFileRecord>(this.StateFileName);
        if (loadResult.TryGetError(out var error, out var loadedRecord))
        {
            // 如果是文件未找到，我们返回一个全新的空记录，而不是错误。
            if (loadResult.ErrorException is FileNotFoundException)
            {
                return new WorkflowPersistenceFileRecord { WorkflowInstanceId = Guid.Parse(this.StateFileName.Replace(".json", "")) };
            }

            return error; // 其他加载错误则直接返回。
        }

        // 如果文件存在但内容为空，也返回一个全新的空记录。
        return loadedRecord ?? new WorkflowPersistenceFileRecord
            { WorkflowInstanceId = Guid.Parse(this.StateFileName.Replace(".json", "")) };
    }

    /// <inheritdoc />
    public async Task<Result<InstanceStateRecord?>> GetStateAsync(Guid instanceId)
    {
        var loadResult = await this.LoadRecordAsync();
        if (loadResult.TryGetError(out var error, out var record))
        {
            return error;
        }

        record.InstanceStates.TryGetValue(instanceId, out var state);
        return Result.Ok(state);
    }

    /// <inheritdoc />
    public async Task<Result> SetStateAsync(InstanceStateRecord stateRecord)
    {
        var loadResult = await this.LoadRecordAsync();
        if (loadResult.TryGetError(out var error, out var record))
        {
            return error;
        }

        // 修改记录
        record.InstanceStates[stateRecord.InstanceId] = stateRecord;

        // 将整个更新后的记录写回文件
        return await this.StorageScope.SaveAllAsync(record, this.StateFileName);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyDictionary<Guid, InstanceStateRecord>>> GetAllStatesAsync()
    {
        var loadResult = await this.LoadRecordAsync();
        if (loadResult.TryGetError(out var error, out var record))
        {
            return error;
        }

        return Result.Ok<IReadOnlyDictionary<Guid, InstanceStateRecord>>(record.InstanceStates);
    }

    /// <summary>
    /// 删除代表此工作流执行的单个持久化文件。
    /// </summary>
    public async Task<Result> ClearAllStatesAsync()
    {
        return await this.StorageScope.DeleteFileAsync(this.StateFileName);
    }
}