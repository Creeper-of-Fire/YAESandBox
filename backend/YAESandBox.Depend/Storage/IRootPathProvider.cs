namespace YAESandBox.Depend.Storage;

/// <summary>
/// 定义一个接口，表示该存储实现知道其操作的根文件路径。
/// 用于缓存等需要基于绝对或唯一基础路径生成键的场景。
/// </summary>
public interface IRootPathProvider
{
    /// <summary>
    /// 获取此存储实例操作的根文件路径。
    /// 主要的作用是用来生成键。
    /// </summary>
    string DataRootPath { get; }
}