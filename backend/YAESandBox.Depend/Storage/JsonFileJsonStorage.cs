using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;
using Nito.AsyncEx;
using NJsonSchema.Annotations;
using YAESandBox.Depend.Results;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 实现 IGeneralJsonStorage 接口，使用 JSON 文件作为底层存储。
/// 这个类负责处理文件的读写和并发访问控制。
/// </summary>
/// <param name="dataRootPath">存储文件的根路径</param>
/// <exception cref="ArgumentNullException"><paramref name="dataRootPath"/>为空</exception>
public class JsonFileJsonStorage(string? dataRootPath) : IGeneralJsonStorage
{
    /// <inheritdoc />
    public string DataRootPath { get; } = dataRootPath ?? throw new ArgumentNullException($"{nameof(dataRootPath)}为空");

    /// <summary>
    /// 并发控制
    /// </summary>
    private ConcurrentDictionary<string, AsyncLock> FilesLocks { get; } = new();

    /// <summary>
    /// 用于控制对单个 Block 的并发访问。
    /// </summary>
    /// <param name="filesPath">文件地址。</param>
    /// <returns></returns>
    private AsyncLock GetLockForFile(FilePath filesPath) => this.FilesLocks.GetOrAdd(filesPath.TotalPath, _ => new AsyncLock());

    private FilePath MakeNewFilePath(string fileName, params string[] subDirectories) => new(this.DataRootPath, fileName, subDirectories);

    private static void EnsureDirectory(FilePath filePath)
    {
        string directory = filePath.TotalDirectory;
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory); // 如果目录不存在，则创建它
    }

    /// <inheritdoc/>
    public async Task<Result<JsonNode?>> LoadJsonNodeAsync(string fileName, params string[] subDirectories)
    {
        var filePath = this.MakeNewFilePath(fileName, subDirectories);
        using (await this.GetLockForFile(filePath).LockAsync()) // 确保文件访问的线程安全
        {
            try
            {
                EnsureDirectory(filePath);

                if (!File.Exists(filePath.TotalPath))
                    return Result.Ok<JsonNode?>(null);

                string json = await File.ReadAllTextAsync(filePath.TotalPath);
                if (string.IsNullOrWhiteSpace(json))
                    return Result.Ok<JsonNode?>(null);

                // 从文件内容反序列化为 JsonNode
                var doc = JsonNode.Parse(json);
                return doc;
            }
            catch (JsonException ex)
            {
                // 捕获 JSON 解析错误
                return Result.Fail($"加载配置时反序列化出错 ({filePath.TotalPath}): {ex.Message}");
            }
            catch (Exception ex) // 捕获其他文件操作等潜在错误
            {
                return Result.Fail($"加载配置时出错 ({filePath.TotalPath}): {ex.Message}");
            }
        }
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

    /// <summary>
    /// 异步保存完整的数据到文件。
    /// 这是一个内部方法，假定外部已获取文件锁。
    /// </summary>
    /// <param name="filePath">文件地址对象。</param>
    /// <param name="configDocument">要保存的配置数据。</param>
    /// <returns>表示操作结果的 Result。</returns>
    private static async Task<Result> SaveAllInternalAsync(FilePath filePath, JsonNode configDocument)
    {
        try
        {
            string directory = filePath.TotalDirectory;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory); // 确保目录存在
            // 将 JsonNode 序列化为 JSON 字符串
            string json = JsonSerializer.Serialize(configDocument, YaeSandBoxJsonHelper.JsonSerializerOptions);
            await File.WriteAllTextAsync(filePath.TotalPath, json);
            return Result.Ok();
        }
        catch (Exception ex) // 捕获文件写入等潜在错误
        {
            return Result.Fail($"保存配置时出错 ({filePath.TotalPath}): {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result> SaveJsonNodeAsync(JsonNode? jsonNode, string fileName, params string[] subDirectories)
    {
        if (jsonNode == null)
            return Result.Ok();
        var filePath = this.MakeNewFilePath(fileName, subDirectories: subDirectories);
        using (await this.GetLockForFile(filePath).LockAsync()) // 确保文件访问的线程安全
        {
            return await SaveAllInternalAsync(filePath, jsonNode);
        }
    }
}

internal record JsonError(string Message) : LazyInitError(Message)
{
    internal static Result Error(string message)
    {
        return new JsonError(message);
    }
}