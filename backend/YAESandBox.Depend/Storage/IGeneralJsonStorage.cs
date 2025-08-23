using YAESandBox.Depend.Results;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 通用的数据存储接口。
/// 负责数据的底层加载和保存，不关心具体结构或内容。
/// </summary>
public interface IGeneralJsonStorage:IWorkPathProvider
{
    /// <summary>
    /// 异步保存完整的数据。
    /// 泛型模式。
    /// </summary>
    /// <param name="needSaveObj">要保存的数据。</param>
    /// <param name="fileName">文件名。</param>
    /// <param name="subDirectories">文件所在的子目录。</param>
    /// <returns>表示操作结果的 Result。</returns>
    Task<Result> SaveAllAsync<T>(T? needSaveObj, string fileName, params string[] subDirectories);

    /// <summary>
    /// 异步加载完整的数据。
    /// 泛型模式。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    /// <param name="subDirectories">文件所在的子目录。</param>
    /// <typeparam name="T">反序列化为的类型</typeparam>
    /// <returns>类型为 T? 的对象，可能为default，如果为空则表示对应的文件没有内容，需要自行处理空值；或表示失败的 Result。</returns>
    Task<Result<T?>> LoadAllAsync<T>(string fileName, params string[] subDirectories);

    /// <summary>
    /// 列出所有可用的文件的名字。
    /// </summary>
    /// <param name="listOption"><see cref="ListFileOption"/>，搜索选项</param>
    /// <param name="subDirectories">需要搜索的子目录。</param>
    /// <returns></returns>
    Task<Result<IEnumerable<string>>> ListFileNamesAsync(ListFileOption? listOption = null, params string[] subDirectories);

    /// <summary>
    /// 删除指定的文件。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    /// <param name="subDirectories">文件所在的子目录。</param>
    /// <returns></returns>
    Task<Result> DeleteFileAsync(string fileName, params string[] subDirectories);

    /// <summary>
    /// 文件筛选选项
    /// </summary>
    public record ListFileOption
    {
        internal static ListFileOption Default { get; } = new();

        /// <summary>
        /// 搜索匹配，仅限简单通配符
        /// </summary>
        public string SearchPattern { get; init; } = "*";

        /// <summary>
        /// 是否支持递归
        /// </summary>
        public bool IsRecursive { get; init; } = false;
    }
}