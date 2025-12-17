using AsyncKeyedLock;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Authentication;

/// <summary>
/// 用户服务，负责用户注册、验证、查找以及刷新令牌的管理。
/// 数据持久化依赖于 <see cref="IGeneralJsonRootStorage"/>。
/// </summary>
public class UserService(IGeneralJsonRootStorage storage, IPasswordService passwordService)
{
    private const string UserFileName = "users.json";

    // 使用 SemaphoreSlim 来防止并发写入同一个用户文件时发生冲突
    private static AsyncNonKeyedLocker FileLock { get; } = new(1);

    /// <summary>
    /// 内部方法，从存储中异步加载所有用户。
    /// </summary>
    /// <returns>返回包含用户字典（Key: UserId, Value: User对象）的结果。</returns>
    private async Task<Result<Dictionary<string, User>>> LoadUsersAsync()
    {
        var loadResult = await storage.LoadAllAsync<Dictionary<string, User>>(UserFileName);
        if (loadResult.TryGetError(out var loadError, out var users))
            return loadError;
        return users ?? new Dictionary<string, User>();
    }

    /// <summary>
    /// 注册新用户。
    /// </summary>
    /// <param name="username">用户名。</param>
    /// <param name="password">密码。</param>
    /// <returns>成功时返回新创建的用户对象，失败时返回错误信息。</returns>
    public async Task<Result<User>> RegisterAsync(string username, string password)
    {
        using var _ = await FileLock.LockAsync();
        var loadResult = await this.LoadUsersAsync();
        if (loadResult.TryGetError(out var loadError, out var users))
            return loadError;

        if (users.Values.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Fail($"用户名 '{username}' 已存在。");
        }

        var newUser = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = username,
            PasswordHash = passwordService.HashPassword(password)
        };

        users[newUser.Id] = newUser;

        var saveResult = await storage.SaveAllAsync(users, UserFileName);
        if (saveResult.TryGetError(out var saveError))
            return saveError;
        return newUser;
    }

    /// <summary>
    /// 验证用户凭证并返回用户对象（登录）。
    /// </summary>
    /// <param name="username">用户名。</param>
    /// <param name="password">用户输入的密码。</param>
    /// <returns>验证成功返回用户对象，失败返回错误信息。</returns>
    public async Task<Result<User>> ValidateUserAsync(string username, string password)
    {
        var loadResult = await this.LoadUsersAsync();
        if (loadResult.TryGetError(out var loadError, out var users))
            return loadError;

        var user = users.Values
            .FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (user is null || !passwordService.VerifyPassword(password, user.PasswordHash))
        {
            return Result.Fail("用户名或密码无效。");
        }

        return Result.Ok(user);
    }

    /// <summary>
    /// 根据用户ID异步查找用户。
    /// </summary>
    /// <param name="userId">用户ID。</param>
    /// <returns>成功时返回用户对象，失败时返回错误信息。</returns>
    public async Task<Result<User>> GetUserByIdAsync(string userId)
    {
        // 这里不需要文件锁，因为我们只是在读取
        var loadResult = await this.LoadUsersAsync();
        if (loadResult.TryGetError(out var loadError, out var users))
            return loadError;

        if (users.TryGetValue(userId, out var user))
            return Result.Ok(user);

        return Result.Fail($"用户ID '{userId}' 未能找到。");
    }

    /// <summary>
    /// 为指定用户保存刷新令牌及其过期时间。
    /// </summary>
    /// <param name="userId">用户ID。</param>
    /// <param name="refreshToken">新的刷新令牌。</param>
    /// <param name="expiryTime">新的过期时间。</param>
    /// <returns>操作成功或失败的结果。</returns>
    public async Task<Result> SaveRefreshTokenAsync(string userId, string refreshToken, DateTime expiryTime)
    {
        using var _ = await FileLock.LockAsync();
        var loadResult = await this.LoadUsersAsync();
        if (loadResult.TryGetError(out var loadError, out var users))
            return loadError;

        if (!users.TryGetValue(userId, out var user))
        {
            return Result.Fail($"用户ID '{userId}' 未能找到。");
        }

        // 更新用户信息
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = expiryTime;

        var saveResult = await storage.SaveAllAsync(users, UserFileName);
        if (saveResult.TryGetError(out var saveError))
            return saveError;

        return Result.Ok();
    }

    /// <summary>
    /// 根据刷新令牌异步查找用户。
    /// 这个方法封装了查找用户的具体实现（当前是遍历 JSON 文件）。
    /// </summary>
    /// <param name="refreshToken">用于查找的刷新令牌。</param>
    /// <returns>成功时返回匹配的用户对象，否则返回错误信息。</returns>
    public async Task<Result<User>> GetUserByRefreshTokenAsync(string refreshToken)
    {
        // 读取操作，不需要文件锁
        var loadResult = await this.LoadUsersAsync();
        if (loadResult.TryGetError(out var loadError, out var users))
        {
            return loadError;
        }

        // 遍历所有用户，查找匹配的刷新令牌
        var user = users.Values.FirstOrDefault(u => u.RefreshToken == refreshToken);

        if (user is not null)
        {
            return Result.Ok(user);
        }

        return Result.Fail("无效的刷新令牌。");
    }
}