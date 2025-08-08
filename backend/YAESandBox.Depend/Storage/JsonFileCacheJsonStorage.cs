using System.Text.Json.Nodes;
using YAESandBox.Depend.Results;

namespace YAESandBox.Depend.Storage;

/// <inheritdoc />
/// <remarks>带缓存的版本</remarks>
public partial class JsonFileCacheJsonStorage(string? dataRootPath) : JsonFileJsonStorage(dataRootPath)
{
    /// <inheritdoc />
    public override async Task<Result<JsonNode?>> LoadJsonNodeAsync(string fileName, params string[] subDirectories)
    {
        if (this.TryRetrieveFileContentInternal(subDirectories, fileName, out var jsonNode))
            return jsonNode?.DeepClone() ?? null;

        // 缓存未命中或克隆失败，从底层存储加载
        var loadResult = await base.LoadJsonNodeAsync(fileName, subDirectories);

        if (!loadResult.TryGetValue(out var loadedDoc))
            return loadResult;
        if (loadedDoc == null) // 找不到键，自行处理空值
            return Result.Ok<JsonNode?>(null);

        // 创建一个副本存入缓存，这样原始的 loadedDoc 可以被调用者安全 Dispose
        this.StoreFileContentInternal(subDirectories, fileName, loadedDoc.DeepClone());
        return loadResult;
    }

    /// <inheritdoc />
    public override async Task<Result> SaveJsonNodeAsync(JsonNode? jsonNode, string fileName, params string[] subDirectories)
    {
        var saveResult = await base.SaveJsonNodeAsync(jsonNode, fileName, subDirectories);
        if (saveResult.TryGetError(out var error))
            return error;
        if (jsonNode == null)
            return saveResult;

        this.StoreFileContentInternal(subDirectories, fileName, jsonNode.DeepClone()); // 创建一个副本存入缓存，以避免外部修改影响缓存
        return saveResult;
    }

    /// <inheritdoc />
    public override async Task<Result> DeleteFileAsync(string fileName, params string[] subDirectories)
    {
        this.RemoveFileFromCacheInternal(subDirectories, fileName);
        return await base.DeleteFileAsync(fileName, subDirectories);
    }
}