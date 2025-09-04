using YAESandBox.Depend.Results;
using static YAESandBox.Depend.Storage.IGeneralJsonStorage;

namespace YAESandBox.Depend.Storage;

/// <inheritdoc />
/// <remarks>带缓存的版本</remarks>
public partial class JsonFileCacheJsonStorage(string? dataRootPath) : JsonFileJsonStorage(dataRootPath)
{
    /// <summary>
    /// 重写文件内容加载逻辑，优先从缓存中读取。
    /// </summary>
    /// <param name="filePath">目标文件的路径对象。</param>
    /// <returns>包含文件内容的 Result，如果缓存命中则来自缓存，否则来自文件系统。</returns>
    protected override async Task<Result<string?>> LoadFileContentAsync(FilePath filePath)
    {
        // 1. 尝试从缓存中获取
        if (this.TryRetrieveFileContentFromCache(filePath, out string? cachedContent))
        {
            return Result.Ok(cachedContent); // 缓存命中
        }

        // 2. 缓存未命中，调用基类方法从磁盘加载
        var loadResult = await base.LoadFileContentAsync(filePath);

        // 3. 如果从磁盘加载成功，将内容存入缓存
        if (loadResult.TryGetValue(out string? fileContent) && fileContent != null)
        {
            this.StoreFileContentInCache(filePath, fileContent);
        }

        return loadResult;
    }

    /// <summary>
    /// 重写文件内容保存逻辑，在成功写入文件后更新缓存。
    /// </summary>
    /// <param name="content">要写入文件的字符串内容。</param>
    /// <param name="filePath">目标文件的路径对象。</param>
    /// <returns>表示操作结果的 Result。</returns>
    protected override async Task<Result> SaveFileContentAsync(string content, FilePath filePath)
    {
        // 1. 调用基类方法将内容写入磁盘
        var saveResult = await base.SaveFileContentAsync(content, filePath);

        // 2. 如果写入成功，则更新缓存
        if (saveResult.IsSuccess)
        {
            this.StoreFileContentInCache(filePath, content);
        }

        return saveResult;
    }

    /// <inheritdoc />
    public override async Task<Result> DeleteFileAsync(string fileName, params string[] subDirectories)
    {
        var filePath = new FilePath(this.WorkPath, fileName, subDirectories);

        // 1. 先从缓存中移除，确保缓存一致性
        this.RemoveFileFromCache(filePath);
        
        // 2. 然后调用基类方法从磁盘删除
        return await base.DeleteFileAsync(fileName, subDirectories);
    }
    
    /// <inheritdoc />
    public override async Task<Result> DeleteDirectoryAsync(DirectoryDeleteOption deleteOption, params string[] subDirectories)
    {
        // 1. 清理缓存中所有位于该目录下的文件
        this.RemoveDirectoryFromCache(subDirectories);
        
        // 2. 调用基类方法从磁盘删除目录
        return await base.DeleteDirectoryAsync(deleteOption, subDirectories);
    }
}