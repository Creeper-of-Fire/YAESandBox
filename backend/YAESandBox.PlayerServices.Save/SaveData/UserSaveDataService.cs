using System.Text.Json.Nodes;
using YAESandBox.Authentication.Storage;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Storage;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;

namespace YAESandBox.PlayerServices.Save.SaveData;

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
    /// 辅助方法，用于获取用户的作用域存储实例。
    /// 路径验证的责任已移至底层存储实现。
    /// </summary>
    private Task<Result<ScopedJsonStorage>> GetStorageAsync(string userId)
    {
        return this.UserStorageFactory.GetFinalStorageForUserAsync(userId, this.ForUserSaves);
    }

    /// <summary>
    /// 辅助方法，确保文件名以 ".json" 结尾，忽略大小写。
    /// </summary>
    private string EnsureJsonExtension(string fileName)
    {
        return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.json";
    }

    /// <summary>
    /// 将一个 JSON 字符串保存到用户存档目录下的指定路径。
    /// </summary>
    public async Task<Result> SaveDataAsync(string userId, string[] directoryParts, string fileName, object jsonObject)
    {
        var storageResult = await this.GetStorageAsync(userId);
        if (storageResult.TryGetError(out var error, out var finalStorage))
        {
            return error;
        }

        string finalFileName = this.EnsureJsonExtension(fileName);
        return await finalStorage.SaveAllAsync(jsonObject, finalFileName, directoryParts);
    }

    /// <summary>
    /// 从用户存档目录中读取指定的 JSON 字符串。
    /// </summary>
    public async Task<Result<object>> ReadDataAsync(string userId, string[] directoryParts, string fileName)
    {
        var storageResult = await this.GetStorageAsync(userId);
        if (storageResult.TryGetError(out var error, out var finalStorage))
        {
            return error;
        }

        string finalFileName = this.EnsureJsonExtension(fileName);
        var result = await finalStorage.LoadAllAsync<JsonNode>(finalFileName, directoryParts);

        if (result.TryGetError(out error, out var value))
            return error;
        if (value == null)
            return string.Empty;
        return value;
    }

    /// <summary>
    /// 删除用户存档目录中指定的 JSON 文件。
    /// </summary>
    public async Task<Result> DeleteDataAsync(string userId, string[] directoryParts, string fileName)
    {
        var storageResult = await this.GetStorageAsync(userId);
        if (storageResult.TryGetError(out var error, out var finalStorage))
        {
            return error;
        }

        string finalFileName = this.EnsureJsonExtension(fileName);
        return await finalStorage.DeleteFileAsync(finalFileName, directoryParts);
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

        return await finalStorage.ListFileNamesAsync(subDirectories: directoryParts);
    }
}