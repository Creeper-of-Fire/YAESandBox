using System.Text.Json;
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
        var filePathResult = this.GetValidatedFilePath(fileName, subDirectories);
        if (filePathResult.TryGetError(out var filePathError, out var filePath))
            return filePathError;

        using (await this.GetLockForFile(filePath).LockAsync())
        {
            return await this.SaveFileContentAsync(jsonContent, filePath);
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result> SaveRawAsync(string rawString, string fileName, params string[] subDirectories)
    {
        var filePathResult = this.GetValidatedFilePath(fileName, subDirectories);
        if (filePathResult.TryGetError(out var filePathError, out var filePath))
            return filePathError;

        using (await this.GetLockForFile(filePath).LockAsync())
        {
            return await this.SaveFileContentAsync(rawString, filePath);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<Result<T?>> LoadAllAsync<T>(string fileName, params string[] subDirectories)
    {
        var filePathResult = this.GetValidatedFilePath(fileName, subDirectories);
        if (filePathResult.TryGetError(out var filePathError, out var filePath))
            return filePathError;

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
            return JsonError.Error(jsonContent, $"反序列化数据为类型 {typeof(T).Name} 时出错。",ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> LoadRawStringAsync(string fileName, params string[] subDirectories)
    {
        var filePathResult = this.GetValidatedFilePath(fileName, subDirectories);
        if (filePathResult.TryGetError(out var filePathError, out var filePath))
            return filePathError;

        string? jsonContent;

        using (await this.GetLockForFile(filePath).LockAsync())
        {
            var loadResult = await this.LoadFileContentAsync(filePath);
            if (loadResult.TryGetError(out var error, out string? content))
                return error;
            jsonContent = content;
        }

        return jsonContent ?? string.Empty;
    }


    /// <inheritdoc />
    public virtual async Task<Result<IEnumerable<string>>> ListFileNamesAsync(
        ListFileOption? listOption = null, params string[] subDirectories)
    {
        var pathResult = this.GetValidatedFullPath(subDirectories);
        if (pathResult.TryGetError(out var error, out var directoryPath))
            return error;

        try // 这个try-catch捕获Task.Run本身可能抛出的问题（例如线程池拒绝）或Task中未处理的异常
        {
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
                    return NormalError.Error($"在后台线程列出文件名时出错 (路径: {directoryPath})。",ex);
                }
            });
        }
        catch (Exception ex) // 捕获 await Task.Run() 可能重新抛出的异常
        {
            return NormalError.Error("列出文件名时发生顶层错误。",ex);
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result<IEnumerable<string>>> ListFoldersAsync(
        ListFileOption? listOption = null, params string[] subDirectories)
    {
        var pathResult = this.GetValidatedFullPath(subDirectories);
        if (pathResult.TryGetError(out var error, out var directoryPath))
            return error;

        try // 这个try-catch捕获Task.Run本身可能抛出的问题（例如线程池拒绝）或Task中未处理的异常
        {
            return await Task.Run(() => // Task.Run<Result<IEnumerable<string>>>
            {
                try // lambda 内部的 try-catch
                {
                    if (!Directory.Exists(directoryPath))
                    {
                        return Result.Ok(Enumerable.Empty<string>());
                    }

                    var options = listOption ?? ListFileOption.Default;

                    IEnumerable<string> files = Directory.GetDirectories(directoryPath,
                        options.SearchPattern,
                        options.IsRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

                    var fileNames = files.Select(Path.GetFileName).WhereNotNull();
                    return Result.Ok(fileNames);
                }
                catch (Exception ex)
                {
                    // 从 lambda 内部返回失败的 Result
                    return NormalError.Error($"在后台线程列出文件名时出错 (路径: {directoryPath})。",ex);
                }
            });
        }
        catch (Exception ex) // 捕获 await Task.Run() 可能重新抛出的异常
        {
            return NormalError.Error("列出文件名时发生顶层错误。",ex);
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result> DeleteFileAsync(string fileName, params string[] subDirectories)
    {
        var filePathResult = this.GetValidatedFilePath(fileName, subDirectories);
        if (filePathResult.TryGetError(out var filePathError, out var filePath))
            return filePathError;

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
                        return Result.Fail($"删除文件 '{filePath.TotalPath}' 时发生IO错误。", ex);
                    }
                    catch (UnauthorizedAccessException ex) // 权限不足
                    {
                        return Result.Fail($"删除文件 '{filePath.TotalPath}' 时权限不足。", ex);
                    }
                    catch (Exception ex) // 其他潜在异常
                    {
                        return Result.Fail($"删除文件 '{filePath.TotalPath}' 时发生未知错误。", ex);
                    }
                });
            }
            catch (Exception ex) // 捕获 LockAsync 或 Task.Run 本身可能抛出的异常
            {
                // 这种情况比较少见，但为了完整性可以捕获
                return Result.Fail($"执行删除文件操作 '{filePath.TotalPath}' 时发生外部错误。", ex);
            }
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result> DeleteDirectoryAsync(DirectoryDeleteOption deleteOption, params string[] subDirectories)
    {
        var pathResult = this.GetValidatedFullPath(subDirectories);
        if (pathResult.TryGetError(out var error, out var directoryPath))
            return error;

        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(directoryPath))
                        return Result.Ok(); // 目录不存在，视为成功

                    switch (deleteOption)
                    {
                        case DirectoryDeleteOption.OnlyIfEmpty:
                            if (Directory.EnumerateFileSystemEntries(directoryPath).Any())
                                return Result.Fail($"无法删除非空目录 '{directoryPath}'。请先清空目录或使用 Recursive 选项。");
                            Directory.Delete(directoryPath, false);
                            break;

                        case DirectoryDeleteOption.RecursiveIfEmpty:
                            return TryDeleteDirectoryRecursiveIfEmpty(directoryPath);

                        case DirectoryDeleteOption.Recursive:
                            Directory.Delete(directoryPath, true);
                            break;
                    }

                    return Result.Ok();
                }
                catch (IOException ex)
                {
                    return Result.Fail($"删除目录 '{directoryPath}' 时发生IO错误。",ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return Result.Fail($"删除目录 '{directoryPath}' 时权限不足。",ex);
                }
                catch (Exception ex)
                {
                    return Result.Fail($"删除目录 '{directoryPath}' 时发生未知错误。",ex);
                }
            });
        }
        catch (Exception ex)
        {
            return Result.Fail($"执行删除目录操作 '{directoryPath}' 时发生外部错误。",ex);
        }
    }

    /// <summary>
    /// 尝试递归删除一个目录，前提是它和它的所有子目录都不包含文件。
    /// </summary>
    /// <param name="directoryPath">要删除的目录的完整路径。</param>
    /// <returns>操作结果。</returns>
    private static Result TryDeleteDirectoryRecursiveIfEmpty(string directoryPath)
    {
        try
        {
            // 1. 遍历所有文件系统条目（文件和子目录）
            foreach (var entry in Directory.EnumerateFileSystemEntries(directoryPath))
            {
                // 2. 如果找到任何文件，立即失败
                if (File.Exists(entry))
                {
                    return Result.Fail($"无法删除目录 '{directoryPath}'，因为它或其子目录包含文件: '{entry}'。");
                }

                // 3. 如果是子目录，进行递归调用
                if (Directory.Exists(entry))
                {
                    var subDirResult = TryDeleteDirectoryRecursiveIfEmpty(entry);
                    // 如果递归失败，立即向上传播失败结果
                    if (subDirResult.TryGetError(out var error))
                        return error;
                }
            }

            // 4. 如果循环完成，说明此目录下所有子目录都已被成功（递归地）删除，且没有文件。
            //    现在可以安全地删除这个空目录了。
            Directory.Delete(directoryPath, false);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"在递归删除空目录 '{directoryPath}' 时发生错误。",ex);
        }
    }
}