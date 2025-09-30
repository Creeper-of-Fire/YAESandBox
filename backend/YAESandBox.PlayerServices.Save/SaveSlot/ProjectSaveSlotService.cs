using YAESandBox.Authentication.Storage;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using static YAESandBox.Depend.Storage.IGeneralJsonStorage;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;
using static YAESandBox.PlayerServices.Save.Utils.TokenUtil;

namespace YAESandBox.PlayerServices.Save.SaveSlot;

/// <summary>
/// 提供基于项目的用户存档槽管理服务。
/// </summary>
public class ProjectSaveSlotService(IUserScopedStorageFactory userStorageFactory)
{
    private const string MetaFileName = "meta.json";
    private IUserScopedStorageFactory UserStorageFactory { get; } = userStorageFactory;



    // --- Public API ---

    /// <summary>
    /// 列出指定项目的所有存档槽。
    /// </summary>
    public async Task<Result<IEnumerable<SaveSlot>>> ListSaveSlotsAsync(string userId, string projectUniqueName)
    {
        var storageResult = await this.GetProjectStorageAsync(userId, projectUniqueName);
        if (storageResult.TryGetError(out var error, out var projectStorage))
            return error;

        var listResult = await projectStorage.ListFoldersAsync();
        if (listResult.TryGetError(out error, out var slotIds))
            return error;

        var slots = new List<SaveSlot>();
        foreach (string slotId in slotIds)
        {
            var metaResult = await projectStorage.LoadAllAsync<SaveSlotMeta>(MetaFileName, slotId);
            if (metaResult.TryGetValue(out var meta) && meta is not null)
            {
                slots.Add(new SaveSlot(
                    Id: slotId,
                    Token: CreateToken(projectUniqueName, slotId),
                    Name: meta.Name,
                    Type: meta.Type,
                    CreatedAt: meta.CreatedAt
                ));
            }
            // else
            // {
            //     slots.Add(new SaveSlot(slotId, "元数据读取失败", "unknown", DateTime.MinValue));
            // }
            // 如果 meta.json 不存在或读取失败，则忽略该目录，因为它不是一个有效的存档槽。
            // TODO 我认为这里可能报错会好一点，但是不清楚。
        }

        return Result.Ok(slots.OrderByDescending(s => s.CreatedAt).AsEnumerable());
    }

    /// <summary>
    /// 创建一个新的存档槽。
    /// </summary>
    public async Task<Result<SaveSlot>> CreateSaveSlotAsync(string userId, string projectUniqueName, CreateSaveSlotRequest request)
    {
        var storageResult = await this.GetProjectStorageAsync(userId, projectUniqueName);
        if (storageResult.TryGetError(out var error, out var projectStorage)) return error;

        string slotId = Guid.NewGuid().ToString("N");
        var meta = new SaveSlotMeta
        {
            Name = request.Name,
            Type = request.Type,
            CreatedAt = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds()
        };

        var saveResult = await projectStorage.SaveAllAsync(meta, MetaFileName, slotId);
        if (saveResult.TryGetError(out error)) return error;

        return Result.Ok(new SaveSlot(
            Id: slotId,
            Token: CreateToken(projectUniqueName, slotId),
            Name: meta.Name,
            Type: meta.Type,
            CreatedAt: meta.CreatedAt
        ));
    }

    /// <summary>
    /// 复制一个现有的存档槽，创建一个全新的槽位。
    /// </summary>
    public async Task<Result<SaveSlot>> CopySaveSlotAsync(string userId, string projectUniqueName, string sourceSlotId, CreateSaveSlotRequest request)
    {
        var storageResult = await this.GetProjectStorageAsync(userId, projectUniqueName);
        if (storageResult.TryGetError(out var error, out var projectStorage))
            return error;

        var copyResult = await CopyDirectoryAsync(projectStorage, sourceSlotId, projectStorage);
        if (copyResult.TryGetError(out error, out string? newSlotId))
            return error;

        // 使用请求中提供的数据创建新的元数据，并覆盖掉刚复制的旧 meta.json
        var newMeta = new SaveSlotMeta
        {
            Name = request.Name,
            Type =  request.Type,
            CreatedAt =  new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds()
        };
        var saveMetaResult = await projectStorage.SaveAllAsync(newMeta, MetaFileName, newSlotId);
        if (saveMetaResult.TryGetError(out error))
        {
            // --- 回滚操作 ---
            // 如果元数据写入失败，我们必须删除在步骤1中刚刚创建的目录，
            // 以避免在文件系统中留下一个不完整、无效的存档槽。
            // 这确保了“从快照创建”这个操作的原子性：要么完全成功，要么完全回滚。
            _ = await projectStorage.DeleteDirectoryAsync(DirectoryDeleteOption.Recursive, newSlotId);

            return error; // 返回写入元数据时发生的原始错误。
        }

        return Result.Ok(new SaveSlot(
            Id: newSlotId,
            Token: CreateToken(projectUniqueName, newSlotId),
            Name: newMeta.Name,
            Type: newMeta.Type,
            CreatedAt: newMeta.CreatedAt
        ));
    }


    /// <summary>
    /// 删除一个存档槽。
    /// </summary>
    public async Task<Result> DeleteSaveSlotAsync(string userId, string projectUniqueName, string slotId)
    {
        var storageResult = await this.GetProjectStorageAsync(userId, projectUniqueName);
        if (storageResult.TryGetError(out var error, out var projectStorage))
        {
            return error;
        }

        return await projectStorage.DeleteDirectoryAsync(DirectoryDeleteOption.Recursive, slotId);
    }


    // --- Private Helpers ---

    private async Task<Result<ScopedJsonStorage>> GetProjectStorageAsync(string userId, string projectUniqueName)
    {
        // 从 'Saves' 根目录开始，创建一个特定于项目的子作用域
        var projectScopeTemplate = SaveRoot().CreateScope(projectUniqueName);
        var storageResult = await this.UserStorageFactory.GetFinalStorageForUserAsync(userId, projectScopeTemplate);
        return storageResult;
    }

    /// <summary>
    /// 辅助方法：将一个目录的内容复制到同一个存储提供者的另一个位置。
    /// </summary>
    /// <param name="storage"></param>
    /// <param name="sourceDir"></param>
    /// <param name="targetStorage"></param>
    /// <returns>新的文件夹的名字</returns>
    private static async Task<Result<string>> CopyDirectoryAsync(IGeneralJsonStorage storage, string sourceDir,
        IGeneralJsonStorage targetStorage)
    {
        var filesResult = await storage.ListFileNamesAsync(ListFileOption.Default, sourceDir);
        if (filesResult.TryGetError(out var error, out var filesToCopy)) return error;

        string newDirectoryName = Guid.NewGuid().ToString("N");

        foreach (string fileName in filesToCopy)
        {
            var contentResult = await storage.LoadRawStringAsync(fileName, sourceDir);
            if (contentResult.TryGetError(out error, out string? content))
                return error;

            var saveResult = await targetStorage.SaveRawAsync(content, fileName, newDirectoryName);
            if (saveResult.TryGetError(out error))
                return error;
        }

        return Result.Ok(newDirectoryName);
    }
}