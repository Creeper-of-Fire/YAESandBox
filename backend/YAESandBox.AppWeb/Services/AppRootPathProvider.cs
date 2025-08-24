using System.Diagnostics;
using YAESandBox.Depend.AspNetCore.Services;

namespace YAESandBox.AppWeb.Services;

/// <summary>
/// IRootPathProvider 的默认实现。
/// 它能够智能检测当前是否运行在打包后的'app'子目录结构中，
/// 并据此计算出正确的物理应用根目录。
/// </summary>
public class AppRootPathProvider : IRootPathProvider
{
    /// <inheritdoc />
    public string RootPath { get; }

    /// <inheritdoc cref="IRootPathProvider"/>
    public AppRootPathProvider(IWebHostEnvironment environment)
    {
        string processName = Process.GetCurrentProcess().ProcessName.ToLowerInvariant();
        bool isDesignTimeTool = processName.Equals("dotnet") || processName.Equals("devenv"); // devenv 是 Visual Studio

        if (isDesignTimeTool)
        {
            // === 设计时工具模式 ===
            this.RootPath = Path.GetTempPath(); 
            Console.WriteLine(
                $"[AppRootPathProvider] Design-time tool '{processName}' detected. Using current working directory as root: {this.RootPath}");
            return;
        }

        if (environment.IsDevelopment())
        {
            // === 开发模式逻辑 ===
            this.RootPath = FindProjectRootDirectory() ?? throw new DirectoryNotFoundException("在开发模式下无法找到项目根目录（.sln 文件所在目录）。");
            Console.WriteLine($"[AppRootPathProvider] Physical Application Root Path resolved to: {this.RootPath}");
            return;
        }

        // 1. 获取当前正在运行的 .exe 文件的目录
        string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (Path.GetDirectoryName(exePath) is not { } exeDir)
        {
            throw new InvalidOperationException("无法找到当前应用程序的运行目录。");
        }

        // 2. 从可执行文件所在目录开始向上搜索
        var searchDir = new DirectoryInfo(exeDir);
        string? foundRootPath = null;
        int maxDepth = 5; // 设置一个最大向上搜索层数，防止死循环

        while (searchDir != null && maxDepth-- > 0)
        {
            // 3. 检查当前目录是否包含名为 "app" 的子目录。
            //    这是打包后应用根目录的可靠标志。
            if (Directory.Exists(Path.Combine(searchDir.FullName, "app")))
            {
                // 找到了！这个目录就是我们需要的应用根目录。
                foundRootPath = searchDir.FullName;
                break;
            }

            // 4. 如果没找到，就移动到父目录继续搜索
            searchDir = searchDir.Parent;
        }
        
        // 5. 最终确定根路径
        if (foundRootPath != null)
        {
            this.RootPath = foundRootPath;
        }
        else
        {
            // 如果向上搜索了5层都没找到，这说明目录结构异常。
            // 此时可以抛出异常，或者作为一个备选方案，使用可执行文件所在的目录。
            // 抛出异常更利于发现问题。
            throw new DirectoryNotFoundException(
                $"无法确定打包后的应用根目录。从 '{exeDir}' 向上查找，未能找到包含 'app' 子目录的文件夹。"
            );
        }

        // 在启动时打印日志，方便调试
        Console.WriteLine($"[AppRootPathProvider] Physical Application Root Path resolved to: {this.RootPath}");
    }

    /// <summary>
    /// 在开发环境中，从当前执行目录向上查找项目根目录（包含 .sln 文件的目录）。
    /// </summary>
    /// <returns>项目根目录的绝对路径，如果找不到则返回 null。</returns>
    private static string? FindProjectRootDirectory()
    {
        // AppContext.BaseDirectory 在开发时通常是 bin/Debug/netX.X/
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        // 循环向上查找，最多10层，防止死循环
        int maxDepth = 10;
        while (currentDirectory != null && maxDepth-- > 0)
        {
            // 检查当前目录是否包含解决方案文件
            if (Directory.GetFiles(currentDirectory.FullName, "*.sln").Length > 0)
            {
                return currentDirectory.FullName;
            }

            // 移动到父目录
            currentDirectory = currentDirectory.Parent;
        }

        return null; // 找不到了
    }
}