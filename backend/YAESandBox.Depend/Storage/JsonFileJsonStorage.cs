using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nito.AsyncEx;
using YAESandBox.Depend.Results;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 实现 IGeneralJsonStorage 接口，使用 JSON 文件作为底层存储。
/// 这个类负责处理文件的读写和并发访问控制。
/// </summary>
/// <param name="dataRootPath">存储文件的根路径</param>
/// <exception cref="ArgumentNullException"><paramref name="dataRootPath"/>为空</exception>
public partial class JsonFileJsonStorage(string? dataRootPath) : IGeneralJsonRootStorage
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
    /// 异步地将字符串内容写入指定文件。
    /// 该方法处理目录创建和文件写入的底层逻辑。
    /// </summary>
    /// <param name="content">要写入文件的字符串内容。</param>
    /// <param name="filePath">目标文件的路径对象。</param>
    /// <returns>表示操作结果的 Result。</returns>
    protected virtual async Task<Result> SaveFileContentAsync(string content, FilePath filePath)
    {
        try
        {
            EnsureDirectory(filePath);
            await File.WriteAllTextAsync(filePath.TotalPath, content);
            return Result.Ok();
        }
        catch (Exception ex) // 捕获文件写入等潜在错误
        {
            return Result.Fail($"保存文件时出错 ({filePath.TotalPath}): {ex.Message}");
        }
    }

    /// <summary>
    /// 异步地从指定文件读取字符串内容。
    /// </summary>
    /// <param name="filePath">目标文件的路径对象。</param>
    /// <returns>包含文件内容的 Result，如果文件不存在则内容为 null；或表示失败的 Result。</returns>
    protected virtual async Task<Result<string?>> LoadFileContentAsync(FilePath filePath)
    {
        try
        {
            if (!File.Exists(filePath.TotalPath))
            {
                return Result.Ok<string?>(null); // 文件不存在，返回成功但内容为null
            }

            return await File.ReadAllTextAsync(filePath.TotalPath);
        }
        catch (Exception ex)
        {
            return Result.Fail($"加载文件时出错 ({filePath.TotalPath}): {ex.Message}");
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