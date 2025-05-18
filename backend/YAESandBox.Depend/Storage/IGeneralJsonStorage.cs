using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;

namespace YAESandBox.Depend.Storage;

/// <summary>
/// 通用的数据存储接口。
/// 负责数据的底层加载和保存，不关心具体结构或内容。
/// </summary>
public interface IGeneralJsonStorage : IRootPathProvider
{
    /// <summary>
    /// 异步加载完整的数据。
    /// 如果文件不存在，则尝试创建包含一个空 JSON 对象的默认文件。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    /// <param name="subDirectories">文件所在的子目录。</param>
    /// <returns>包含数据的 JsonNode? ，如果为空则表示对应的文件没有内容，需要自行处理空值；或表示失败的 Result。</returns>
    Task<Result<JsonNode?>> LoadJsonNodeAsync(string fileName, params string[] subDirectories);

    /// <summary>
    /// 异步保存完整的数据。
    /// </summary>
    /// <param name="jsonNode">要保存的数据。</param>
    /// <param name="fileName">文件名。</param>
    /// <param name="subDirectories">文件所在的子目录。</param>
    /// <returns>表示操作结果的 Result。</returns>
    Task<Result> SaveJsonNodeAsync(JsonNode? jsonNode, string fileName, params string[] subDirectories);

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
}

// /// <summary>
// /// 装饰器，用于对 IGeneralJsonStorage 进行装饰
// /// 注意，它内部应该有解除/覆盖下层 <see cref="IGeneralJsonStorageStatelessDecorator"/> 装饰器的方法，所以它应当是无状态的，最多只能有一些初始化逻辑和只读字段。
// /// </summary>
// internal interface IGeneralJsonStorageStatelessDecorator
// {
//     /// <summary>
//     /// 原始的IGeneralJsonStorage
//     /// </summary>
//     public IGeneralJsonStorage GeneralJsonStorage { get; }
//
//     public static IGeneralJsonStorage UnNestJsonDecorator(IGeneralJsonStorage generalJsonStorage) =>
//         generalJsonStorage is IGeneralJsonStorageStatelessDecorator scopedJsonStorage
//             ? scopedJsonStorage.GeneralJsonStorage
//             : generalJsonStorage;
// }