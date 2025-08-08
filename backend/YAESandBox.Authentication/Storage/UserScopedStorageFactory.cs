using System.Collections.Concurrent;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using static YAESandBox.Depend.Storage.ScopedStorageFactory;

namespace YAESandBox.Authentication.Storage;

/// <summary>
/// 初始化 UserScopedStorageFactory 的一个新实例。继承自 <see cref="IUserScopedStorageFactory"/>
/// </summary>
/// <param name="rootStorage">指向应用根数据目录的通用存储服务。</param>
/// <param name="userService">用于验证用户身份的服务。</param>
public class UserScopedStorageFactory(
    IGeneralJsonRootStorage rootStorage,
    UserService userService
) : IUserScopedStorageFactory
{
    private IGeneralJsonStorage RootStorage { get; } = rootStorage;
    private UserService UserService { get; } = userService;

    // 缓存已创建的用户存储实例，以提高性能
    private ConcurrentDictionary<string, IGeneralJsonStorage> UserStorageCache { get; } = new();


    /// <inheritdoc />
    public async Task<Result<IGeneralJsonStorage>> GetStorageForUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result.Fail("User ID cannot be null or empty.");
        }

        // 1. 检查缓存
        if (this.UserStorageCache.TryGetValue(userId, out var cachedStorage))
        {
            return Result.Ok(cachedStorage);
        }

        // 2. 缓存未命中，验证用户是否存在
        var userResult = await this.UserService.GetUserByIdAsync(userId);
        if (userResult.TryGetError(out var error, out var user))
        {
            return error; // 用户不存在或加载用户时出错
        }

        // 3. 用户存在，创建沙箱化存储实例
        // 我们重用现有的 ScopedStorageFactory 来创建一个新的作用域，其名称就是用户的ID
        var userScopedStorage = this.RootStorage.CreateScope(user.Id);

        // 4. 存入缓存并返回
        this.UserStorageCache.TryAdd(userId, userScopedStorage);

        return userScopedStorage;
    }

    /// <inheritdoc />
    public async Task<Result<ScopedJsonStorage>> GetFinalStorageForUserAsync(string userId, ScopeTemplate template)
    {
        // 1. 复用现有逻辑获取用户的根存储
        var userRootStorageResult = await this.GetStorageForUserAsync(userId);
        if (userRootStorageResult.TryGetError(out var error, out var userRootStorage))
            return error;

        // 2. 将模板应用到获取到的用户根存储上
        var finalStorage = template.ApplyOn(userRootStorage);

        // 3. 返回成功的结果
        return Result.Ok(finalStorage);
    }
}