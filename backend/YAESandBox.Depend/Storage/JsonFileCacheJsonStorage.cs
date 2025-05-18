using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 带有缓存的 JsonStorage，用于装饰 <see cref="IGeneralJsonStorage"/> ，最好用它来装饰最顶层的 JsonStorage。
/// 尽管它只是一个装饰器，但是由于它内部有缓存内容，所以不是无状态的。
/// </summary>
/// <param name="generalJsonStorage"></param>
public class JsonFileCacheJsonStorage(IGeneralJsonStorage generalJsonStorage) : IGeneralJsonStorage
{
    private IGeneralJsonStorage GeneralJsonStorage { get; } = generalJsonStorage;

    private static string GetCacheKey(FilePath filePath) => filePath.TotalPath;

    /// <inheritdoc />
    public string DataRootPath { get; } = generalJsonStorage.DataRootPath;

    private ConcurrentDictionary<string, JsonNode> Cache { get; } = new();

    private Lock CacheLock { get; } = new();

    /// <inheritdoc />
    public async Task<Result<JsonNode?>> LoadJsonNodeAsync(string fileName, params string[] subDirectories)
    {
        FilePath filePath = new(this.DataRootPath, fileName, subDirectories);
        string cacheKey = GetCacheKey(filePath);
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
        var loadResult = await this.GeneralJsonStorage.LoadJsonNodeAsync(fileName, subDirectories);

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
    public async Task<Result> SaveJsonNodeAsync(JsonNode? jsonNode, string fileName, params string[] subDirectories)
    {
        var saveResult = await this.GeneralJsonStorage.SaveAllAsync(jsonNode, fileName, subDirectories);
        if (saveResult.IsFailed)
            return saveResult;

        // 创建一个副本存入缓存，以避免外部修改影响缓存
        if (jsonNode == null)
            return saveResult;
        if (!jsonNode.CloneJsonNode(out var docToCache))
            return saveResult;
        FilePath filePath = new(this.DataRootPath, fileName, subDirectories);
        string cacheKey = GetCacheKey(filePath);
        lock (this.Cache)
        {
            this.Cache[cacheKey] = docToCache;
        }

        return saveResult;
    }

    /// <inheritdoc/>
    public async Task<Result> SaveAllAsync<T>(T? needSaveObj, string fileName, params string[] subDirectories)
    {
        var json = JsonSerializer.SerializeToNode(needSaveObj, YaeSandBoxJsonHelper.JsonSerializerOptions);
        return await this.SaveJsonNodeAsync(json, fileName, subDirectories);
    }

    /// <inheritdoc/>
    public async Task<Result<T?>> LoadAllAsync<T>(string fileName, params string[] subDirectories)
    {
        try
        {
            var jsonResult = await this.LoadJsonNodeAsync(fileName, subDirectories);
            if (!jsonResult.TryGetValue(out var value))
                return jsonResult.ToResult();
            if (value == null)
                return Result.Ok<T?>(default);

            var obj = value.Deserialize<T>(YaeSandBoxJsonHelper.JsonSerializerOptions);
            if (obj is null)
                return JsonError.Error($"反序列化数据为类型 {typeof(T).Name} 时出错: 序列化结果为 null");
            return obj;
        }
        catch (JsonException ex)
        {
            return JsonError.Error($"反序列化数据为类型 {typeof(T).Name} 时出错: {ex.Message}");
        }
        catch (Exception ex) // 捕获其他文件操作等潜在错误
        {
            return JsonError.Error($"反序列化数据为类型 {typeof(T).Name} 时出错: {ex.Message}");
        }
    }
}