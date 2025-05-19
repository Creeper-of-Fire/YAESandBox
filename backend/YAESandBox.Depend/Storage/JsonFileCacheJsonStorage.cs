using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;

namespace YAESandBox.Depend.Storage;

/// <inheritdoc />
/// <remarks>带缓存的版本</remarks>
public class JsonFileCacheJsonStorage(string? dataRootPath) : JsonFileJsonStorage(dataRootPath)
{
    private string GetCacheKey(string fileName, params string[] subDirectories) =>
        FilePath.CombinePath(this.WorkPath, fileName, subDirectories);

    private ConcurrentDictionary<string, JsonNode> Cache { get; } = new();

    private Lock CacheLock { get; } = new();

    /// <inheritdoc />
    public override async Task<Result<JsonNode?>> LoadJsonNodeAsync(string fileName, params string[] subDirectories)
    {
        string cacheKey = this.GetCacheKey(fileName, subDirectories);
        JsonNode? jsonNode;
        lock (this.CacheLock)
        {
            this.Cache.TryGetValue(cacheKey, out jsonNode);
        }


        // 注意：如果返回缓存的 JsonNode，调用者和缓存共享同一个实例。
        // JsonNode 是只读的，所以共享读取没问题。
        // 我们采用克隆策略保证隔离。
        if (jsonNode != null)
        {
            // 创建一个新的 JsonNode 实例作为副本返回
            if (jsonNode.CloneJsonNode(out var clonedDocument))
                return clonedDocument;

            // 克隆失败，可能原始文档有问题或已释放，准备从文件重新加载
            // 清除可能已损坏的缓存条目
            lock (this.CacheLock)
            {
                this.Cache.TryRemove(cacheKey, out _);
            }
        }


        // 缓存未命中或克隆失败，从底层存储加载
        var loadResult = await base.LoadJsonNodeAsync(fileName, subDirectories);

        // 将新加载的（或其副本）存入缓存
        // 为了缓存一致性，最好是存入一个副本，或者原始的（如果生命周期由缓存管理）

        if (!loadResult.TryGetValue(out var loadedDoc))
            return loadResult;
        if (loadedDoc == null) // 找不到键，自行处理空值
            return Result.Ok<JsonNode?>(null);

        // 创建一个副本存入缓存，这样原始的 loadedDoc 可以被调用者安全 Dispose
        // 即使克隆失败，也继续返回原始加载的文档，只是不缓存
        if (!loadedDoc.CloneJsonNode(out var docToCache))
            return loadResult;

        lock (this.CacheLock)
        {
            this.Cache[cacheKey] = docToCache;
        }

        return loadResult;
    }

    /// <inheritdoc />
    public override async Task<Result> SaveJsonNodeAsync(JsonNode? jsonNode, string fileName, params string[] subDirectories)
    {
        var saveResult = await base.SaveJsonNodeAsync(jsonNode, fileName, subDirectories);
        if (saveResult.IsFailed)
            return saveResult;

        // 创建一个副本存入缓存，以避免外部修改影响缓存
        if (jsonNode == null)
            return saveResult;
        if (!jsonNode.CloneJsonNode(out var docToCache))
            return saveResult;
        string cacheKey = this.GetCacheKey(fileName, subDirectories);
        lock (this.Cache)
        {
            this.Cache[cacheKey] = docToCache;
        }

        return saveResult;
    }
}