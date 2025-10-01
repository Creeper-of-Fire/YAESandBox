using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;

namespace YAESandBox.Authentication.Storage;

/// <summary>
/// 一个工厂服务，用于为特定用户提供一个沙箱化的 IGeneralJsonStorage 实例。
/// 所有针对该实例的操作都将被限定在该用户的专属数据目录（例如 /Data/{UserId}/）内。
/// </summary>
public interface IUserScopedStorageFactory
{
    /// <summary>
    /// 异步地为指定用户ID获取一个沙箱化的存储服务实例。
    /// 此方法会验证用户ID的有效性，并返回一个配置好作用域的 IGeneralJsonStorage。
    /// </summary>
    /// <param name="userId">要获取存储服务的用户ID。</param>
    /// <returns>
    /// 一个 Result 对象，成功时包含沙箱化的 IGeneralJsonStorage 实例，
    /// 失败时（例如用户不存在）包含错误信息。
    /// </returns>
    Task<Result<IGeneralJsonStorage>> GetStorageForUserAsync(string userId);

    /// <summary>
    /// 异步地为指定用户ID获取一个应用了作用域模板的沙箱化存储服务实例。
    /// 它会自动的在这个静态的 <see cref="ScopeTemplate"/> 模板中插入 <paramref name="userId"/>，实现完善的作用域管理，而不需要手动拼接。
    /// 这是获取用户特定子目录存储的首选方法。
    /// </summary>
    /// <param name="userId">要获取存储服务的用户ID。</param>
    /// <param name="template">要应用到用户根目录的作用域模板。</param>
    /// <returns>
    /// 一个 Result 对象，成功时包含最终的 ScopedJsonStorage 实例，
    /// 失败时（例如用户不存在）包含错误信息。
    /// </returns>
    Task<Result<ScopedJsonStorage>> GetFinalStorageForUserAsync(string userId, ScopeTemplate template);
}