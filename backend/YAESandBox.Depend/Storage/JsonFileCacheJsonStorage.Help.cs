using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace YAESandBox.Depend.Storage;

public partial class JsonFileCacheJsonStorage
{
    private DirectoryCacheEntry Cache { get; } = new("/");
    private Lock CacheLock { get; } = new();

    /// <summary>
    /// 导航到缓存中指定的目录条目。
    /// </summary>
    /// <param name="directoryPathParts">组成目标目录的路径部分 (相对于缓存根)。</param>
    /// <param name="createIfNotExists">如果目录不存在，是否创建它。</param>
    /// <returns>找到或创建的 DirectoryCacheEntry，如果路径无效且不允许创建则返回 null。</returns>
    private DirectoryCacheEntry? NavigateToDirectoryEntryInternal(string[] directoryPathParts, bool createIfNotExists)
    {
        lock (this.CacheLock) // 保护对父目录Children集合的修改和树结构的整体性
        {
            var currentDirectory = this.Cache;

            // 如果 directoryPathParts 为空或null，表示根目录自身
            if (directoryPathParts.Length == 0)
                return this.Cache;

            foreach (string part in directoryPathParts.Where(part => !string.IsNullOrEmpty(part) && part != "." && part != "/"))
            {
                if (part == "..")
                {
                    Log.Error("缓存模型不支持 \"..\" 向上导航");
                    return null;
                }

                // 尝试获取子目录
                if (currentDirectory.Children.TryGetValue(part, out var entry))
                {
                    if (entry is not DirectoryCacheEntry dirEntry)
                    {
                        Log.Error($"路径冲突: '{string.Join("/", directoryPathParts)}' 中的 '{part}' 是文件，而不是目录，这是一个无效的目录路径。");
                        return null;
                    }

                    currentDirectory = dirEntry; // 跳转到子目录
                    continue;
                }

                // 子目录不存在
                if (!createIfNotExists)
                    return null; // 不允许创建，且目录不存在

                // 确实不存在，创建新目录
                var newDirEntry = new DirectoryCacheEntry(part);
                currentDirectory.Children.TryAdd(part, newDirEntry); // ConcurrentDictionary 的 TryAdd
                currentDirectory = newDirEntry;
            }

            return currentDirectory;
        }
    }

    /// <summary>
    /// 将文件内容（作为JsonNode）存储或更新到缓存中。
    /// </summary>
    /// <param name="directoryPathParts">文件所在目录的路径部分。</param>
    /// <param name="fileName">文件名。</param>
    /// <param name="contentNode">要存储的JsonNode内容。null表示文件逻辑上为空或不存在对应内容。</param>
    /// <returns>如果操作成功，返回true；否则false。</returns>
    private bool StoreFileContentInternal(string[] directoryPathParts, string fileName, JsonNode? contentNode)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false; // 文件名不能为空

        DirectoryCacheEntry? targetDirectory;
        // 使用锁来保护导航和可能的目录创建过程
        lock (this.CacheLock)
        {
            targetDirectory = this.NavigateToDirectoryEntryInternal(directoryPathParts, createIfNotExists: true);
        }

        if (targetDirectory == null)
        {
            Log.Error("Failed to navigate to or create target directory in cache for storing file.");
            return false; // 无法找到或创建目标目录
        }

        // 现在我们有了目标目录的 DirectoryCacheEntry，可以安全地操作其 Children
        // ConcurrentDictionary 本身对单个操作（如 TryGetValue, TryAdd, AddOrUpdate）是线程安全的
        if (!targetDirectory.Children.TryGetValue(fileName, out var existingEntry))
        {
            // 文件不存在，新建文件条目
            targetDirectory.Children.TryAdd(fileName, new FileCacheEntry(fileName, contentNode));
            return true;
        }

        if (existingEntry is not FileCacheEntry fileEntry)
        {
            // 名称冲突：缓存中已存在同名的目录
            targetDirectory.Children.TryUpdate(fileName, new FileCacheEntry(fileName, contentNode), existingEntry);
            return true;
        }

        // 文件已存在，更新内容
        fileEntry.ContentNode = contentNode; // JsonNode 是引用类型，直接赋值
        // 如果 contentNode 是从外部传入的，且担心外部修改，
        // 这里可以存入 contentNode.DeepClone()

        return true;
    }

    /// <summary>
    /// 从缓存中检索文件内容。
    /// </summary>
    /// <param name="directoryPathParts">文件所在目录的路径部分。</param>
    /// <param name="fileName">文件名。</param>
    /// <param name="contentNode">如果找到文件且有内容，则输出JsonNode。</param>
    /// <returns>true 如果在缓存中找到了文件条目（即使内容为null），false 如果连文件条目都找不到。</returns>
    private bool TryRetrieveFileContentInternal(string[] directoryPathParts, string fileName, out JsonNode? contentNode)
    {
        contentNode = null;
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        DirectoryCacheEntry? targetDirectory;
        lock (this.CacheLock) // 为导航加上锁，确保一致性视图
        {
            targetDirectory = this.NavigateToDirectoryEntryInternal(directoryPathParts, createIfNotExists: false);
        }


        if (targetDirectory == null)
            return false; // 目录未在缓存中找到

        if (!targetDirectory.Children.TryGetValue(fileName, out var entry) || entry is not FileCacheEntry fileEntry)
            return false; // 文件条目未在缓存中找到，名称存在但不是文件（是目录），也视为找不到文件
        contentNode = fileEntry.ContentNode; // 文件条目找到，返回其内容（可能是null）
        return true;
    }

    /// <summary>
    /// 从缓存中移除指定的文件条目。
    /// </summary>
    /// <param name="directoryPathParts">文件所在目录的路径部分。</param>
    /// <param name="fileName">要删除的文件名。</param>
    /// <returns>如果文件被成功从缓存中移除，或者文件原本就不在缓存中，则返回 true。如果因路径无效或名称冲突（例如，尝试删除一个实际是目录的条目）导致无法执行删除语义，则返回 false。</returns>
    private void RemoveFileFromCacheInternal(string[] directoryPathParts, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;

        DirectoryCacheEntry? targetDirectory;
        // 导航到目录的过程需要保护，以防其他线程正在修改树结构
        lock (this.CacheLock)
        {
            targetDirectory = this.NavigateToDirectoryEntryInternal(directoryPathParts, createIfNotExists: false);
        }

        if (targetDirectory == null)
            return; //幂等

        // 尝试从目标目录的 Children 中移除文件条目
        // ConcurrentDictionary.TryRemove 是线程安全的
        if (!targetDirectory.Children.TryGetValue(fileName, out _))
            return; //幂等

        // 无论返回值是什么类型的，我们直接删掉，因为我们持有的仅仅只是缓存而已，无关痛痒。
        targetDirectory.Children.TryRemove(fileName, out _);
    }

    internal abstract record CacheEntry(string Name)
    {
        public DateTime LastAccessed { get; set; } // 用于可能的LRU等策略
        // public DateTime LastModifiedFromFile { get; set; } // 用于检测文件是否已更改（高级）
    }

    internal record FileCacheEntry(string Name, JsonNode? ContentNode) : CacheEntry(Name)
    {
        public JsonNode? ContentNode { get; set; } = ContentNode;
        // 或者用 string JsonContent {get; set;}
    }

    internal record DirectoryCacheEntry(string Name) : CacheEntry(Name)
    {
        // 子条目，键是名称，值是 CacheEntry (文件或子目录)
        public ConcurrentDictionary<string, CacheEntry> Children { get; } = new(StringComparer.OrdinalIgnoreCase); // Windows下通常不区分大小写
    }
}