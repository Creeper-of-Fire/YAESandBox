using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using static YAESandBox.Depend.Storage.IGeneralJsonStorage;

namespace YAESandBox.Depend.AspNetCore.Secret;

/// <summary>
/// 一个装饰器（Decorator），用于为任何 IGeneralJsonRootStorage 实现提供自动的数据加密和解密功能。
/// 它包装一个底层的存储实例，并在数据持久化之前加密，在数据加载之后解密。
/// </summary>
/// <param name="UnderlyingStorage">被包装的底层存储实例，可以是 JsonFileJsonStorage 或 JsonFileCacheJsonStorage 等。</param>
/// <param name="DataProtectionService">用于执行加密和解密操作的服务。</param>
public record ProtectedJsonStorage(
    IGeneralJsonRootStorage UnderlyingStorage,
    IDataProtectionService DataProtectionService) : IGeneralJsonRootStorage
{
    /// <inheritdoc />
    public string WorkPath => this.UnderlyingStorage.WorkPath;

    /// <inheritdoc />
    /// <remarks>加载文件时，会先调用底层存储加载数据，然后对数据进行解密。</remarks>
    public async Task<Result<T?>> LoadAllAsync<T>(string fileName, params string[] subDirectories)
    {
        // 1. 调用底层存储加载数据（此时数据可能是加密的）
        var loadResult = await this.UnderlyingStorage.LoadAllAsync<T>(fileName, subDirectories);

        // 如果加载失败或数据为空，则直接返回
        if (!loadResult.TryGetValue(out var dataObject) || dataObject is null)
            return loadResult;

        var unprotectResult = this.DataProtectionService.Unprotect(dataObject);
        if (unprotectResult.TryGetError(out var unprotectError, out var unprotectObject))
            return unprotectError;

        return unprotectObject;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 不应当用于可能被加密/解密的数据。
    /// </remarks>
    public async Task<Result<string>> LoadRawStringAsync(string fileName, params string[] subDirectories)
    {
        // 无法进行解密，因此直接调用底层存储加载原始字符串
        return await this.UnderlyingStorage.LoadRawStringAsync(fileName, subDirectories);
    }

    /// <inheritdoc />
    /// <remarks>保存文件时，会先对数据进行加密，然后调用底层存储保存加密后的数据。</remarks>
    public async Task<Result> SaveAllAsync<T>(T? needSaveObj, string fileName, params string[] subDirectories)
    {
        if (needSaveObj is null)
            return await this.UnderlyingStorage.SaveAllAsync(needSaveObj, fileName, subDirectories);

        var protectResult = this.DataProtectionService.Protect(needSaveObj);
        if (protectResult.TryGetError(out var protectError, out var protectObject))
            return protectError;

        // 2. 调用底层存储来持久化已经加密过的对象
        return await this.UnderlyingStorage.SaveAllAsync(protectObject, fileName, subDirectories);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 不应当用于可能被加密/解密的数据。
    /// </remarks>
    public Task<Result> SaveRawAsync(string rawString, string fileName, params string[] subDirectories)
    {
        // 无法进行加密，因此直接调用底层存储保存原始字符串
        return this.UnderlyingStorage.SaveRawAsync(rawString, fileName, subDirectories);
    }

    /// <inheritdoc />
    /// <remarks>列出文件名不涉及文件内容，直接穿透到底层存储。</remarks>
    public Task<Result<IEnumerable<string>>> ListFileNamesAsync(ListFileOption? listOption = null, params string[] subDirectories) =>
        this.UnderlyingStorage.ListFileNamesAsync(listOption, subDirectories);

    /// <inheritdoc />
    /// <remarks>列出文件夹名不涉及文件内容，直接穿透到底层存储。</remarks>   
    public Task<Result<IEnumerable<string>>> ListFoldersAsync(ListFileOption? listOption = null, params string[] subDirectories) =>
        this.UnderlyingStorage.ListFoldersAsync(listOption, subDirectories);

    /// <inheritdoc />
    /// <remarks>删除文件不涉及文件内容，直接穿透到底层存储。</remarks>
    public Task<Result> DeleteFileAsync(string fileName, params string[] subDirectories) =>
        this.UnderlyingStorage.DeleteFileAsync(fileName, subDirectories);

    /// <inheritdoc />
    /// <remarks>删除文件夹不涉及文件内容，直接穿透到底层存储。</remarks>
    public Task<Result> DeleteDirectoryAsync(DirectoryDeleteOption deleteOption, params string[] subDirectories) =>
        this.UnderlyingStorage.DeleteDirectoryAsync(deleteOption, subDirectories);
}