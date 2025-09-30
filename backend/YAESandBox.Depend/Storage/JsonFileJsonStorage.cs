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

    private Result<FilePath> GetValidatedFilePath(string fileName, params string[] subDirectories)
    {
        string[] allParts = subDirectories.Append(fileName).ToArray();
        var validationResult = this.GetValidatedFullPath(allParts);
        
        if (validationResult.TryGetError(out var error))
            return error;

        return new FilePath(this.WorkPath, fileName, subDirectories) ;
    }

    /// <summary>
    /// 验证给定的路径部分是否安全，并返回位于 WorkPath 内的完整绝对路径。
    /// </summary>
    /// <param name="pathParts">文件名和子目录的集合。</param>
    /// <returns>成功时返回包含完整路径的 Result，失败时返回包含错误信息的 Result。</returns>
    private Result<string> GetValidatedFullPath(params string[] pathParts)
    {
        // 1. 检查每个路径部分是否包含非法字符或遍历序列
        foreach (string part in pathParts)
        {
            if (string.IsNullOrEmpty(part)) continue; // 允许空部分，Path.Combine会处理
            if (part.Contains("..") || part.Contains(':') || part.Contains('/') || part.Contains('\\'))
            {
                return Result.Fail($"路径部分 '{part}' 包含非法的目录遍历或绝对路径序列。");
            }
            if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return Result.Fail($"路径部分 '{part}' 包含非法字符。");
            }
        }

        // 2. 组合路径
        string combinedPath = Path.Combine([this.WorkPath, ..pathParts]);

        // 3. 规范化路径并进行最终验证
        string fullWorkPath = Path.GetFullPath(this.WorkPath);
        string fullFinalPath = Path.GetFullPath(combinedPath);

        if (!fullFinalPath.StartsWith(fullWorkPath, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail("路径遍历攻击被阻止。最终路径超出了允许的工作目录范围。");
        }

        return Result.Ok(fullFinalPath);
    }
    
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
            return Result.Fail($"保存文件时出错 ({filePath.TotalPath})。",ex);
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
            return Result.Fail($"加载文件时出错 ({filePath.TotalPath})。",ex);
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
            return Result.Fail($"保存配置时出错 ({filePath.TotalPath})。",ex);
        }
    }
}