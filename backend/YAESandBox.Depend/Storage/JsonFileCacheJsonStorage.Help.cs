using System.Collections.Concurrent;

namespace YAESandBox.Depend.Storage;

public partial class JsonFileCacheJsonStorage
{
    /// <summary>
    /// 缓存存储。键是文件的相对路径（例如 "Saves/slot1.json"），值是文件内容的缓存条目。
    /// 使用 ConcurrentDictionary 以实现线程安全。
    /// </summary>
    private ConcurrentDictionary<string, FileCacheEntry> Cache { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 将文件内容存储到缓存中。
    /// </summary>
    /// <param name="filePath">文件的路径对象。</param>
    /// <param name="content">要缓存的文件JSON字符串内容。</param>
    private void StoreFileContentInCache(FilePath filePath, string content)
    {
        // 使用文件的子路径（相对于WorkPath）作为缓存键
        string cacheKey = filePath.SubPath;
        var entry = new FileCacheEntry(content, DateTime.UtcNow);

        // AddOrUpdate 提供了原子性的添加或更新操作
        this.Cache.AddOrUpdate(cacheKey, entry, (_, _) => entry);
    }

    /// <summary>
    /// 尝试从缓存中检索文件内容。
    /// </summary>
    /// <param name="filePath">文件的路径对象。</param>
    /// <param name="content">如果找到，则输出缓存的JSON字符串内容。</param>
    /// <returns>如果缓存命中，则为 true；否则为 false。</returns>
    private bool TryRetrieveFileContentFromCache(FilePath filePath, out string? content)
    {
        content = null;
        string cacheKey = filePath.SubPath;

        if (!this.Cache.TryGetValue(cacheKey, out var entry))
            return false;

        // 这里可以加入过期逻辑，例如：
        // if (DateTime.UtcNow - entry.CachedAtUtc > TimeSpan.FromMinutes(30)) {
        //     this.Cache.TryRemove(cacheKey, out _);
        //     return false; // 缓存已过期
        // }
        content = entry.Content;
        return true;
    }

    /// <summary>
    /// 从缓存中移除指定的文件。
    /// </summary>
    /// <param name="filePath">要移除的文件的路径对象。</param>
    private void RemoveFileFromCache(FilePath filePath)
    {
        string cacheKey = filePath.SubPath;
        this.Cache.TryRemove(cacheKey, out _);
    }
    
    /// <summary>
    /// 从缓存中移除指定目录下的所有文件条目。
    /// </summary>
    /// <param name="subDirectories">要移除的目录路径部分。</param>
    private void RemoveDirectoryFromCache(params string[] subDirectories)
    {
        if (subDirectories.Length == 0)
        {
            this.Cache.Clear(); // 如果是根目录，则清空所有缓存
            return;
        }

        string directoryPrefix = Path.Combine(subDirectories) + Path.DirectorySeparatorChar;

        var keysToRemove = this.Cache.Keys.Where(key => 
            key.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var key in keysToRemove)
        {
            this.Cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 代表缓存中一个文件的条目。
    /// </summary>
    /// <param name="Content">文件的原始JSON字符串内容。</param>
    /// <param name="CachedAtUtc">条目被缓存时的UTC时间。</param>
    internal record FileCacheEntry(string Content, DateTime CachedAtUtc);
}