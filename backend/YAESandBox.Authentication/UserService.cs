using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;

namespace YAESandBox.Authentication;

public class UserService(IGeneralJsonStorage storage, IPasswordService passwordService)
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
}