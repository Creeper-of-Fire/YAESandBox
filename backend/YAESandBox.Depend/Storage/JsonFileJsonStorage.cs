using System.Collections.Concurrent;
using System.Text.Json;
using FluentResults;
using Nito.AsyncEx;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 实现 IGeneralJsonStorage 接口，使用 JSON 文件作为底层存储。
/// 这个类负责处理文件的读写和并发访问控制。
/// </summary>
/// <param name="dataRootPath">存储文件的根路径</param>
/// <exception cref="ArgumentNullException"><paramref name="dataRootPath"/>为空</exception>
public class JsonFileJsonStorage(string? dataRootPath) : IGeneralJsonStorage
{
    /// <summary>
    /// 存储文件的根路径
    /// </summary>
    private string DataRootPath { get; } = dataRootPath ?? throw new ArgumentNullException($"{nameof(dataRootPath)}为空");

    /// <summary>
    /// 并发控制
    /// </summary>
    private ConcurrentDictionary<string, AsyncLock> filesLocks { get; } = new();

    /// <summary>
    /// 用于控制对单个 Block 的并发访问。
    /// </summary>
    /// <param name="filesPath">文件地址。</param>
    /// <returns></returns>
    private AsyncLock GetLockForFile(FilePath filesPath) => this.filesLocks.GetOrAdd(filesPath.SubPath, _ => new AsyncLock());

    private FilePath MakeNewFilePath(string directory, string fileName) => new(this.DataRootPath, directory, fileName);

    private static void EnsureDirectory(FilePath filePath)
    {
        string directory = filePath.TotalDirectory;
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory); // 如果目录不存在，则创建它
    }

    /// <inheritdoc/>
    public async Task<Result<JsonDocument?>> LoadAllAsync(string subDirectory, string fileName)
    {
        JsonDocument getEmptyDoc() => JsonDocument.Parse("{}");
        var filePath = this.MakeNewFilePath(subDirectory, fileName);
        using (await this.GetLockForFile(filePath).LockAsync()) // 确保文件访问的线程安全
        {
            try
            {
                EnsureDirectory(filePath);

                if (!File.Exists(filePath.TotalPath))
                {
                    // 文件不存在，创建默认的空 JSON 对象文件
                    var result = await SaveAllInternalAsync(filePath, getEmptyDoc());
                    if (result.IsFailed)
                        return JsonError.Error($"文件{filePath}不存在，并且在尝试新建默认文件时失败。");
                    return Result.Ok<JsonDocument?>(null); // 返回新创建的空文档
                }

                string json = await File.ReadAllTextAsync(filePath.TotalPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    // 文件为空，返回一个包含空 JSON 对象的文档
                    return Result.Ok<JsonDocument?>(null);
                }

                // 从文件内容反序列化为 JsonDocument
                var doc = JsonDocument.Parse(json);
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
    public async Task<Result> SaveAllAsync<T>(string subDirectory, string fileName, T needSaveObj)
    {
        var json = JsonSerializer.SerializeToDocument(needSaveObj, YAESandBoxJsonHelper.JsonSerializerOptions);
        return await this.SaveAllAsync(subDirectory, fileName, json);
    }

    /// <inheritdoc/>
    public async Task<Result<T?>> LoadAllAsync<T>(string subDirectory, string fileName)
    {
        try
        {
            var jsonResult = await this.LoadAllAsync(subDirectory, fileName);
            if (jsonResult.IsFailed)
                return jsonResult.ToResult();
            if (jsonResult.Value == null)
                return Result.Ok<T?>(default);

            var obj = jsonResult.Value.Deserialize<T>(YAESandBoxJsonHelper.JsonSerializerOptions);
            if (obj == null)
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
    private static async Task<Result> SaveAllInternalAsync(FilePath filePath, JsonDocument configDocument)
    {
        try
        {
            string directory = filePath.TotalDirectory;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory); // 确保目录存在
            // 将 JsonDocument 序列化为 JSON 字符串
            string json = JsonSerializer.Serialize(configDocument.RootElement, YAESandBoxJsonHelper.JsonSerializerOptions);
            await File.WriteAllTextAsync(filePath.TotalPath, json);
            return Result.Ok();
        }
        catch (Exception ex) // 捕获文件写入等潜在错误
        {
            return Result.Fail($"保存配置时出错 ({filePath.TotalPath}): {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result> SaveAllAsync(string subDirectory, string fileName, JsonDocument jsonDocument)
    {
        var filePath = this.MakeNewFilePath(subDirectory, fileName);
        using (await this.GetLockForFile(filePath).LockAsync()) // 确保文件访问的线程安全
        {
            return await SaveAllInternalAsync(filePath, jsonDocument);
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

internal record FilePath(string RootPath, string SubDirectory, string FileName)
{
    internal string TotalPath => Path.Combine(this.RootPath, this.SubDirectory, this.FileName);
    internal string TotalDirectory => Path.Combine(this.RootPath, this.SubDirectory);

    internal string SubPath => Path.Combine(this.SubDirectory, this.FileName);
    public override string ToString() => this.TotalPath;
}