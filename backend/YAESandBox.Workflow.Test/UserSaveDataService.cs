using System.Text.Json.Nodes;
using YAESandBox.Authentication.Storage;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Storage;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;

namespace YAESandBox.Workflow.Test;

/// <summary>
/// 提供用户自定义存档数据的存储服务。
/// 所有数据都将被存储在用户的 'Saves' 根目录下。
/// </summary>
public class UserSaveDataService(IUserScopedStorageFactory userStorageFactory)
{
    private IUserScopedStorageFactory UserStorageFactory { get; } = userStorageFactory;

    // 定义存储作用域模板，指向 SaveRoot()
    private ScopeTemplate ForUserSaves { get; } = SaveRoot();

    /// <summary>
    /// 辅助方法，用于验证路径、拆分文件名和目录，并获取作用域存储实例。
    /// </summary>
    private async Task<(Result<ScopedJsonStorage> storageResult, string fileName, string[] subDirectories)>
        GetStorageAndPathParts(string userId, string[] pathParts)
    {
        if (pathParts.Length == 0)
        {
            return (NormalError.BadRequest("路径不能为空。"), string.Empty, []);
        }

        if (pathParts.Any(p => string.IsNullOrWhiteSpace(p) || p.Contains("..") || p.Contains('/') || p.Contains('\\')))
        {
            return (NormalError.BadRequest("路径包含无效字符或目录遍历尝试。"), string.Empty, []);
        }

        string fileName = pathParts.Last();
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".json";
        }

        string[] subDirectories = pathParts.Take(pathParts.Length - 1).ToArray();

        var storageResult = await this.UserStorageFactory.GetFinalStorageForUserAsync(userId, this.ForUserSaves);
        return (storageResult, fileName, subDirectories);
    }

    /// <summary>
    /// 将一个 JSON 字符串保存到用户存档目录下的指定路径。
    /// </summary>
    public async Task<Result> SaveDataAsync(string userId, string[] pathParts, string jsonData)
    {
        var (storageResult, fileName, subDirectories) = await GetStorageAndPathParts(userId, pathParts);
        if (storageResult.TryGetError(out var error, out var finalStorage))
        {
            return error;
        }

        return await finalStorage.SaveAllAsync(jsonData, fileName, subDirectories);
    }

    /// <summary>
    /// 从用户存档目录中读取指定的 JSON 字符串。
    /// </summary>
    public async Task<Result<string>> ReadDataAsync(string userId, string[] pathParts)
    {
        var (storageResult, fileName, subDirectories) = await GetStorageAndPathParts(userId, pathParts);
        if (storageResult.TryGetError(out var error, out var finalStorage))
        {
            return error;
        }

        var result = await finalStorage.LoadAllAsync<string>(fileName, subDirectories);
        if (result.TryGetError(out error, out var value))
            return error;
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value;
    }

    /// <summary>
    /// 删除用户存档目录中指定的 JSON 文件。
    /// </summary>
    public async Task<Result> DeleteDataAsync(string userId, string[] pathParts)
    {
        var (storageResult, fileName, subDirectories) = await GetStorageAndPathParts(userId, pathParts);
        if (storageResult.TryGetError(out var error, out var finalStorage))
        {
            return error;
        }

        return await finalStorage.DeleteFileAsync(fileName, subDirectories);
    }

    /// <summary>
    /// 列出用户存档目录下指定子目录中的所有 JSON 文件名。
    /// </summary>
    public async Task<Result<IEnumerable<string>>> ListDataAsync(string userId, string[] directoryParts)
    {
        // 列表操作不涉及文件名，因此直接获取存储
        var storageResult = await this.UserStorageFactory.GetFinalStorageForUserAsync(userId, this.ForUserSaves);
        if (storageResult.TryGetError(out var error, out var finalStorage))
        {
            return error;
        }

        // 验证目录路径
        if (directoryParts.Any(p => string.IsNullOrWhiteSpace(p) || p.Contains("..") || p.Contains('/') || p.Contains('\\')))
        {
            return NormalError.BadRequest("路径包含无效字符或目录遍历尝试。");
        }

        return await finalStorage.ListFileNamesAsync(subDirectories: directoryParts);
    }
}