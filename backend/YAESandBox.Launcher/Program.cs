using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices; // 用于弹窗
using System.Threading.Tasks;
using YAESandBox.Launcher;
using static YAESandBox.Launcher.Logger;

try
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    // 1. 确定路径
// =================================
    string? launcherExePath = Process.GetCurrentProcess().MainModule?.FileName;
    string? rootDir = Path.GetDirectoryName(launcherExePath);

    if (string.IsNullOrEmpty(rootDir))
    {
        ShowError("无法确定启动器所在的目录。");
        return;
    }

// =================================
// === 更新逻辑 ===
// =================================
    try
    {
        // 配置你的更新服务器地址
        const string LATEST_VERSION_URL = "https://your-server.com/releases/latest-version.json";
        var updater = new Updater(rootDir, LATEST_VERSION_URL);
        await updater.CheckForUpdatesAsync();
    }
    catch (Exception ex)
    {
        // 在这里，我们只显示错误，但仍然尝试启动旧版本
        // 生产环境中你可能想阻止启动，或者给用户选择
        Console.WriteLine($"❌ 更新失败: {ex.Message}");
        ShowError($"更新过程中发生错误，将尝试启动当前版本。\n\n错误详情: {ex.Message}");
    }
// =================================
// === 更新结束 ===
// =================================


    const string appSubDir = "app";
    const string mainAppName = "YAESandBox.exe";
    string mainAppPath = Path.Combine(rootDir, appSubDir, mainAppName);

    if (!File.Exists(mainAppPath))
    {
        ShowError($"启动失败：未找到主应用程序。\n\n路径：{mainAppPath}\n\n请确认您的安装是否完整。");
        return;
    }

// 2. 准备启动信息
// =================================
    var processStartInfo = new ProcessStartInfo
    {
        FileName = mainAppPath,
        WorkingDirectory = Path.GetDirectoryName(mainAppPath), // **非常重要**：设置工作目录为主程序所在目录
        UseShellExecute = false, // **必须为 false** 才能重定向输出
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true, // 启动器本身不创建黑框窗口 (需要项目类型为 Windows Application)
    };

// 将启动器的命令行参数传递给主程序
    foreach (string arg in args)
    {
        processStartInfo.ArgumentList.Add(arg);
    }

// 3. 启动并监控进程
// =================================
    try
    {
        string logDir = Path.Combine(rootDir, "Logs");
        Directory.CreateDirectory(logDir);
        string logFilePath = Path.Combine(logDir, $"launch-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log");

        // logWriter 的生命周期现在清晰地包围了所有操作
        await using var logWriter = new StreamWriter(logFilePath, true, System.Text.Encoding.UTF8);
        using var process = new Process();
        process.StartInfo = processStartInfo;
        logWriter.WriteLine($"--- 启动器日志于 {DateTime.Now} ---");
        logWriter.WriteLine($"启动主程序: {mainAppPath}");

        process.Start();

        // === 核心修改：创建独立的任务来处理流读取 ===
        var outputReaderTask = ReadStreamAsync(process.StandardOutput, logWriter, "[INFO]");
        var errorReaderTask = ReadStreamAsync(process.StandardError, logWriter, "[ERROR]");

        // 等待所有任务完成：进程退出、标准输出读完、标准错误读完
        await Task.WhenAll(process.WaitForExitAsync(), outputReaderTask, errorReaderTask);

        logWriter.WriteLine($"--- 主程序已退出，退出码: {process.ExitCode} ---");
    }
    catch (Exception ex)
    {
        // 捕获启动过程中的致命错误 (例如权限问题)
        string errorMessage = $"启动过程中发生严重错误：\n\n{ex.Message}\n\n请检查日志文件获取详细信息。";
        ShowError(errorMessage, Path.Combine(rootDir, "Logs"));
    }
}
catch (Exception ex)
{
    // 捕获启动过程中的非致命错误
    string errorMessage = $"启动过程中发生错误：\n\n{ex.Message}\n\n请检查日志文件获取详细信息。";
    Console.WriteLine(errorMessage);
}