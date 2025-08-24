using System.Security.Cryptography;
using System.Text.Json;

namespace YAESandBox.Launcher;

public class Updater
{
    private readonly string _rootDir;
    private readonly string _latestVersionUrl;
    private readonly HttpClient _httpClient;

    // 本地版本文件，简单起见，我们直接在 app 目录下放一个 version.json
    private readonly string _localVersionFilePath;

    public Updater(string rootDir, string latestVersionUrl)
    {
        _rootDir = rootDir;
        _latestVersionUrl = latestVersionUrl;
        _httpClient = new HttpClient();
        _localVersionFilePath = Path.Combine(_rootDir, "app", "version.json");
    }

    public async Task CheckForUpdatesAsync()
    {
        Console.WriteLine("🚀 [更新检查] 开始检查更新...");

        // 1. 获取本地版本
        string localVersion = GetLocalVersion();
        Console.WriteLine($"  - 本地版本: {localVersion}");

        // 2. 获取远程最新版本信息
        LatestVersionInfo? remoteVersionInfo;
        try
        {
            string json = await _httpClient.GetStringAsync(_latestVersionUrl);
            remoteVersionInfo = JsonSerializer.Deserialize<LatestVersionInfo>(json, AppJsonSerializerContext.Default.LatestVersionInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ 无法获取远程版本信息: {ex.Message}");
            return;
        }

        if (remoteVersionInfo is null)
        {
            Console.WriteLine("  ❌ 无法获取远程版本信息。");
            return;
        }

        Console.WriteLine($"  - 最新版本: {remoteVersionInfo.Version}");

        // 3. 比较版本
        if (new Version(localVersion) >= new Version(remoteVersionInfo.Version))
        {
            Console.WriteLine("  ✅ 您已是最新版本。");
            return;
        }

        Console.WriteLine($"  ✨ 发现新版本 {remoteVersionInfo.Version}！准备更新...");

        // 4. 获取远程清单
        Console.WriteLine("  - 正在下载版本清单...");
        var remoteManifest = await FetchManifestAsync(remoteVersionInfo.ManifestUrl);
        if (remoteManifest is null)
        {
            Console.WriteLine("  ❌ 无法获取远程清单。");
            return;
        }

        // 5. 生成本地清单
        Console.WriteLine("  - 正在扫描本地文件...");
        var localManifest = GenerateLocalManifest();

        // 6. 计算差异
        var (toDownload, toDelete) = CalculateDiff(localManifest, remoteManifest);
        if (!toDownload.Any() && !toDelete.Any())
        {
            Console.WriteLine("  - 文件已是最新，无需更新。");
            await this.UpdateLocalVersionFile(remoteManifest.Version); // 版本号不一致但文件一致，也更新版本号
            return;
        }

        long totalDownloadSize = toDownload.Sum(f => f.Value.Size);
        Console.WriteLine($"  - 需要下载 {toDownload.Count} 个文件 (共 {totalDownloadSize / 1024.0 / 1024.0:F2} MB)。");
        Console.WriteLine($"  - 需要删除 {toDelete.Count} 个文件。");

        // 7. 下载文件
        string updateTempDir = Path.Combine(_rootDir, "_update");
        Directory.CreateDirectory(updateTempDir);
        await DownloadFilesAsync(toDownload, remoteManifest.BaseUrl, updateTempDir);

        // 8. 应用更新
        Console.WriteLine("  - 正在应用更新...");
        ApplyUpdate(toDelete, updateTempDir);

        // 9. 更新本地版本文件
        await UpdateLocalVersionFile(remoteManifest.Version);

        Console.WriteLine($"✅ 更新完成！已更新至版本 {remoteManifest.Version}。");
    }

    private string GetLocalVersion()
    {
        if (!File.Exists(_localVersionFilePath)) return "0.0.0";
        string json = File.ReadAllText(_localVersionFilePath);
        var versionInfo = JsonSerializer.Deserialize<LatestVersionInfo>(json, AppJsonSerializerContext.Default.LatestVersionInfo);
        return versionInfo?.Version ?? "0.0.0";
    }

    private async Task UpdateLocalVersionFile(string newVersion)
    {
        var versionInfo = new VersionOnly { Version = newVersion };
        string json = JsonSerializer.Serialize(versionInfo, AppJsonSerializerContext.Default.VersionOnly);
        await File.WriteAllTextAsync(_localVersionFilePath, json);
    }

    private async Task<Manifest?> FetchManifestAsync(string url)
    {
        string json = await _httpClient.GetStringAsync(url);
        return JsonSerializer.Deserialize<Manifest>(json, AppJsonSerializerContext.Default.Manifest);
    }

    private Manifest GenerateLocalManifest()
    {
        var files = new Dictionary<string, FileEntry>();
        var filesToScan = Directory.EnumerateFiles(_rootDir, "*", SearchOption.AllDirectories);

        foreach (string file in filesToScan)
        {
            string relativePath = Path.GetRelativePath(_rootDir, file).Replace('\\', '/');
            // 忽略日志、临时更新文件和git文件
            if (relativePath.StartsWith("Logs/", StringComparison.Ordinal) ||
                relativePath.StartsWith("_update/", StringComparison.Ordinal) || relativePath.Contains(".git"))
            {
                continue;
            }

            files[relativePath] = new FileEntry
            {
                Hash = CalculateFileHash(file),
                Size = new FileInfo(file).Length
            };
        }

        return new Manifest { Files = files };
    }

    private (Dictionary<string, FileEntry> toDownload, List<string> toDelete) CalculateDiff(Manifest local, Manifest? remote)
    {
        var toDownload = new Dictionary<string, FileEntry>();
        var remoteFiles = remote.Files;
        var localFiles = local.Files;

        foreach (var remoteFile in remoteFiles)
        {
            if (!localFiles.ContainsKey(remoteFile.Key) || localFiles[remoteFile.Key].Hash != remoteFile.Value.Hash)
            {
                toDownload.Add(remoteFile.Key, remoteFile.Value);
            }
        }

        var toDelete = localFiles.Keys.Except(remoteFiles.Keys).ToList();
        return (toDownload, toDelete);
    }

    private async Task DownloadFilesAsync(Dictionary<string, FileEntry> files, string baseUrl, string tempDir)
    {
        int count = 0;
        foreach (var file in files)
        {
            count++;
            string url = baseUrl + file.Key;
            string destPath = Path.Combine(tempDir, file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? throw new InvalidOperationException());

            Console.Write($"  - 下载中 ({count}/{files.Count}): {file.Key}");

            using var response = await this._httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);

            // 校验哈希
            string downloadedHash = CalculateFileHash(destPath);
            if (downloadedHash != file.Value.Hash)
            {
                throw new Exception($"文件校验失败: {file.Key}。期望哈希 {file.Value.Hash}，实际哈希 {downloadedHash}。");
            }

            Console.WriteLine(" ... ✓");
        }
    }

    private void ApplyUpdate(List<string> toDelete, string tempDir)
    {
        // 删除文件
        foreach (string file in toDelete)
        {
            string path = Path.Combine(_rootDir, file);
            if (File.Exists(path))
            {
                File.Delete(path);
                Console.WriteLine($"  - 已删除: {file}");
            }
        }

        // 移动新文件
        foreach (string file in Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(tempDir, file);
            string destPath = Path.Combine(_rootDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
            File.Move(file, destPath, true);
            Console.WriteLine($"  - 已更新: {relativePath}");
        }

        Directory.Delete(tempDir, true);
    }

    private string CalculateFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hash = sha256.ComputeHash(stream);
        return Convert.ToHexStringLower(hash);
    }
}