using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Authentication;

public class UserService(IGeneralJsonRootStorage storage, IPasswordService passwordService)
{
    private const string UserFileName = "users.json";

    // 使用 SemaphoreSlim 来防止并发写入同一个用户文件时发生冲突
    private static SemaphoreSlim FileLock { get; } = new(1, 1);

    // 内部方法，加载所有用户
    private async Task<Result<Dictionary<string, User>>> LoadUsersAsync()
    {
        var loadResult = await storage.LoadAllAsync<Dictionary<string, User>>(UserFileName);
        if (loadResult.TryGetError(out var loadError, out var users))
            return loadError;
        return users ?? new Dictionary<string, User>();
    }

    // 注册新用户
    public async Task<Result<User>> RegisterAsync(string username, string password)
    {
        await FileLock.WaitAsync();
        try
        {
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
        finally
        {
            FileLock.Release();
        }
    }

    // 验证用户凭证并返回用户对象
    public async Task<Result<User>> ValidateUserAsync(string username, string password)
    {
        var loadResult = await this.LoadUsersAsync();
        if (loadResult.TryGetError(out var loadError, out var users))
            return loadError;

        var user = users.Values
            .FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (user == null || !passwordService.VerifyPassword(password, user.PasswordHash))
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
        await FileLock.WaitAsync();
        try
        {
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
        finally
        {
            FileLock.Release();
        }
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

        if (user != null)
        {
            return Result.Ok(user);
        }

        return Result.Fail("无效的刷新令牌。");
    }
}