using System.Text.Json;
using System.Text.Json.Nodes;
using Nito.Disposables.Internals;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using static YAESandBox.Depend.Storage.IGeneralJsonStorage;

namespace YAESandBox.Depend.Storage;

public partial class JsonFileJsonStorage
{
    /// <inheritdoc/>
    public virtual async Task<Result> SaveAllAsync<T>(T? needSaveObj, string fileName, params string[] subDirectories)
    {
        string jsonContent = JsonSerializer.Serialize(needSaveObj, YaeSandBoxJsonHelper.JsonSerializerOptions);
        var filePath = this.GetNewFilePath(fileName, subDirectories);
        using (await this.GetLockForFile(filePath).LockAsync())
        {
            return await this.SaveFileContentAsync(jsonContent, filePath);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<Result<T?>> LoadAllAsync<T>(string fileName, params string[] subDirectories)
    {
        var filePath = this.GetNewFilePath(fileName, subDirectories);
        string? jsonContent;

        using (await this.GetLockForFile(filePath).LockAsync())
        {
            var loadResult = await this.LoadFileContentAsync(filePath);
            if (loadResult.TryGetError(out var error, out string? content))
                return error;
            jsonContent = content;
        }

        if (string.IsNullOrWhiteSpace(jsonContent))
            return Result.Ok<T?>(default);

        try
        {
            var obj = JsonSerializer.Deserialize<T>(jsonContent, YaeSandBoxJsonHelper.JsonSerializerOptions);
            return Result.Ok(obj);
        }
        catch (JsonException ex)
        {
            return JsonError.Error(jsonContent, $"反序列化数据为类型 {typeof(T).Name} 时出错: {ex.Message}");
        }
    }


    /// <inheritdoc />
    public virtual async Task<Result<IEnumerable<string>>> ListFileNamesAsync(
        ListFileOption? listOption = null, params string[] subDirectories)
    {
        try // 这个try-catch捕获Task.Run本身可能抛出的问题（例如线程池拒绝）或Task中未处理的异常
        {
            string directoryPath = Path.Combine(this.WorkPath, Path.Combine(subDirectories ?? []));

            return await Task.Run(() => // Task.Run<Result<IEnumerable<string>>>
            {
                try // lambda 内部的 try-catch
                {
                    if (!Directory.Exists(directoryPath))
                    {
                        return Result.Ok(Enumerable.Empty<string>());
                    }

                    var options = listOption ?? ListFileOption.Default;

                    IEnumerable<string> files = Directory.GetFiles(directoryPath,
                        options.SearchPattern,
                        options.IsRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

                    var fileNames = files.Select(Path.GetFileName).WhereNotNull();
                    return Result.Ok(fileNames);
                }
                catch (Exception ex)
                {
                    // 从 lambda 内部返回失败的 Result
                    return NormalError.Error($"在后台线程列出文件名时出错 (路径: {directoryPath}): {ex.Message}");
                }
            });
        }
        catch (Exception ex) // 捕获 await Task.Run() 可能重新抛出的异常
        {
            return NormalError.Error($"列出文件名时发生顶层错误: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result> DeleteFileAsync(string fileName, params string[] subDirectories)
    {
        var filePath = this.GetNewFilePath(fileName, subDirectories);

        // 2. 获取文件锁，确保删除操作的原子性和避免竞争条件
        //    即使是删除，也最好获取与读写相同的锁，以防其他操作正在访问该文件。
        using (await this.GetLockForFile(filePath).LockAsync())
        {
            try
            {
                // 3. 将同步的 File.Delete 操作包装在 Task.Run 中
                return await Task.Run(() =>
                {
                    try // Task.Run 内部的 try-catch
                    {
                        if (!File.Exists(filePath.TotalPath))
                            return Result.Ok(); // 通常，删除一个不存在的文件不应视为错误。
                        File.Delete(filePath.TotalPath);
                        return Result.Ok();
                    }
                    catch (IOException ex) // 例如，文件被占用
                    {
                        return Result.Fail($"删除文件 '{filePath.TotalPath}' 时发生IO错误: {ex.Message}");
                    }
                    catch (UnauthorizedAccessException ex) // 权限不足
                    {
                        return Result.Fail($"删除文件 '{filePath.TotalPath}' 时权限不足: {ex.Message}");
                    }
                    catch (Exception ex) // 其他潜在异常
                    {
                        return Result.Fail($"删除文件 '{filePath.TotalPath}' 时发生未知错误: {ex.Message}");
                    }
                });
            }
            catch (Exception ex) // 捕获 LockAsync 或 Task.Run 本身可能抛出的异常
            {
                // 这种情况比较少见，但为了完整性可以捕获
                return Result.Fail($"执行删除文件操作 '{filePath.TotalPath}' 时发生外部错误: {ex.Message}");
            }
        }
    }
}