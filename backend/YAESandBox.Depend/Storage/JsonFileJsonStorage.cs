using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;
using Nito.AsyncEx;
using YAESandBox.Depend.Results;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 实现 IGeneralJsonStorage 接口，使用 JSON 文件作为底层存储。
/// 这个类负责处理文件的读写和并发访问控制。
/// </summary>
/// <param name="dataRootPath">存储文件的根路径</param>
/// <exception cref="ArgumentNullException"><paramref name="dataRootPath"/>为空</exception>
public partial class JsonFileJsonStorage(string? dataRootPath) : IGeneralJsonStorage
{
    /// <inheritdoc />
    public string WorkPath { get; } =
        dataRootPath ?? throw new ArgumentNullException($"{nameof(JsonFileJsonStorage)}的{nameof(dataRootPath)}为空");

    /// <summary> 并发控制 </summary>
    private ConcurrentDictionary<string, AsyncLock> FilesLocks { get; } = new();

    /// <summary>
    /// 用于控制对单个 Block 的并发访问。
    /// </summary>
    /// <param name="filesPath">文件地址。</param>
    /// <returns></returns>
    private AsyncLock GetLockForFile(FilePath filesPath) => this.FilesLocks.GetOrAdd(filesPath.TotalPath, _ => new AsyncLock());

    private FilePath GetNewFilePath(string fileName, params string[] subDirectories) => new(this.WorkPath, fileName, subDirectories);

    private static void EnsureDirectory(FilePath filePath)
    {
        string directory = filePath.TotalDirectory;
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory); // 如果目录不存在，则创建它
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
            EnsureDirectory(filePath);
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
}

internal record JsonError(string Message) : LazyInitError(Message)
{
    internal static Result Error(string message)
    {
        return new JsonError(message);
    }
}