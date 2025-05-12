using System.Text.Json;
using FluentResults;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 通用的数据存储接口。
/// 负责数据的底层加载和保存，不关心具体结构或内容。
/// </summary>
public interface IGeneralJsonStorage
{
    /// <summary>
    /// 异步加载完整的数据。
    /// 如果文件不存在，则尝试创建包含一个空 JSON 对象的默认文件。
    /// </summary>
    /// <param name="subDirectory">文件所在的子目录。</param>
    /// <param name="fileName">文件名。</param>
    /// <returns>包含数据的 JsonDocument? ，如果为空则表示对应的文件没有内容，需要自行处理空值；或表示失败的 Result。</returns>
    Task<Result<JsonDocument?>> LoadAllAsync(string subDirectory, string fileName);

    /// <summary>
    /// 异步保存完整的数据。
    /// </summary>
    /// <param name="subDirectory">文件所在的子目录。</param>
    /// <param name="fileName">文件名。</param>
    /// <param name="jsonDocument">要保存的数据。</param>
    /// <returns>表示操作结果的 Result。</returns>
    Task<Result> SaveAllAsync(string subDirectory, string fileName, JsonDocument jsonDocument);

    /// <summary>
    /// 异步保存完整的数据。
    /// 泛型模式。
    /// </summary>
    /// <param name="subDirectory">文件所在的子目录。</param>
    /// <param name="fileName">文件名。</param>
    /// <param name="needSaveObj">要保存的数据。</param>
    /// <returns>表示操作结果的 Result。</returns>
    Task<Result> SaveAllAsync<T>(string subDirectory, string fileName, T needSaveObj);

    /// <summary>
    /// 异步加载完整的数据。
    /// 泛型模式。
    /// </summary>
    /// <param name="subDirectory">文件所在的子目录。</param>
    /// <param name="fileName">文件名。</param>
    /// <typeparam name="T">反序列化为的类型</typeparam>
    /// <returns>类型为 T? 的对象，可能为default，如果为空则表示对应的文件没有内容，需要自行处理空值；或表示失败的 Result。</returns>
    Task<Result<T?>> LoadAllAsync<T>(string subDirectory, string fileName);
}