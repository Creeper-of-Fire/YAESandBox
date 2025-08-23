namespace YAESandBox.Depend.AspNetCore.Services;

/// <summary>
/// 提供应用程序的物理根目录路径。
/// 这个路径代表了用户可见的、最顶层的应用文件夹，
/// 与 IWebHostEnvironment.ContentRootPath（指向Web服务器内容）有所区别。
/// </summary>
public interface IRootPathProvider
{
    /// <summary>
    /// 获取应用程序的物理根目录的绝对路径。
    /// 在“游戏化”的打包结构中，这将是包含启动器.exe的目录。
    /// 在开发环境中，它可能是项目目录。
    /// </summary>
    string RootPath { get; }
}