using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using YAESandBox.Depend.AspNetCore;

namespace YAESandBox.Authentication;

/// <summary>
/// 登录请求的数据模型。
/// </summary>
public record LoginRequest
{
    /// <summary>
    /// 用户名
    /// </summary>
    [Required(ErrorMessage = "用户名不能为空")]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    [Required(ErrorMessage = "密码不能为空")]
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// 刷新令牌请求的数据模型。
/// </summary>
public record RefreshTokenRequest
{
    /// <summary>
    /// 
    /// </summary>
    [Required]
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// 
    /// </summary>
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// 注册请求的数据模型。
/// </summary>
public record RegisterRequest
{
    /// <summary>
    /// 用户名
    /// </summary>
    [Required(ErrorMessage = "用户名不能为空")]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    [Required(ErrorMessage = "密码不能为空")]
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// 成功认证后的响应数据模型。
/// </summary>
public record AuthResponse
{
    /// <summary>
    /// 
    /// </summary>
    [Required]
    public required string Token { get; init; } = string.Empty;

    /// <summary>
    /// 
    /// </summary>
    [Required]
    public required string RefreshToken { get; init; } = string.Empty;

    /// <summary>
    /// 
    /// </summary>
    [Required]
    public required string UserId { get; init; } = string.Empty;

    /// <summary>
    /// 
    /// </summary>
    [Required]
    public required string Username { get; init; } = string.Empty;
}

/// <summary>
/// 提供用户注册和登录的端点。
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[ApiExplorerSettings(GroupName = AuthenticationModule.AuthenticationGroupName)]
public class AuthController(UserService userService, IConfiguration configuration) : ControllerBase
{
    private const double RefreshTokenExpiryDays = 15;
    private const double AccessTokenExpiryHours = 24;
    private UserService UserService { get; } = userService;
    private IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// 用户注册。
    /// </summary>
    /// <param name="request">包含用户名和密码的注册信息。</param>
    /// <returns>注册成功或失败的消息。</returns>
    /// <response code="200">用户注册成功。</response>
    /// <response code="400">请求数据无效（例如用户名已存在）或输入为空。</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(typeof(string), 400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await this.UserService.RegisterAsync(request.Username, request.Password);
        if (result.TryGetValue(out var user))
            return this.Ok($"用户 '{user.Username}' 注册成功。");
        return this.Get500ErrorResult(result);
    }

    /// <summary>
    /// 用户登录并获取JWT和刷新令牌。
    /// </summary>
    /// <param name="request">包含用户名和密码的登录信息。</param>
    /// <returns>包含JWT、用户ID和用户名的认证响应。</returns>
    /// <response code="200">登录成功，返回认证信息。</response>
    /// <response code="401">用户名或密码无效。</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(typeof(string), 401)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var userResult = await this.UserService.ValidateUserAsync(request.Username, request.Password);

        if (userResult.TryGetError(out var error, out var user))
            return this.Unauthorized(error);

        // 生成 Access Token
        string token = this.GenerateJwtToken(user);

        // 生成并保存 Refresh Token
        string refreshToken = this.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays);
        var result = await this.UserService.SaveRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiry);
        if (result.TryGetError(out var saveError))
            return this.Get500ErrorResult(saveError);

        return this.Ok(new AuthResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            UserId = user.Id,
            Username = user.Username,
        });
    }

    /// <summary>
    /// 使用刷新令牌和用户ID获取新的访问令牌。
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(typeof(string), 401)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        // 1. 直接通过 UserId 高效查找用户
        var userResult = await this.UserService.GetUserByIdAsync(request.UserId);
        if (!userResult.TryGetValue(out var user))
        {
            // 如果用户ID无效，则未授权
            return this.Unauthorized("用户无效或不存在。");
        }

        // 2. 验证该用户的 RefreshToken 是否匹配，以及是否过期
        if (user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            // 如果令牌不匹配或已过期，则未授权
            return this.Unauthorized("刷新令牌与用户不匹配或已过期。");
        }

        // 3. 生成新的 Access Token 和 Refresh Token
        string newAccessToken = this.GenerateJwtToken(user);
        string newRefreshToken = this.GenerateRefreshToken();
        var newRefreshTokenExpiry = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays);

        // 4. 更新用户的 Refresh Token
        var result = await this.UserService.SaveRefreshTokenAsync(user.Id, newRefreshToken, newRefreshTokenExpiry);
        if (result.TryGetError(out var saveError))
            return this.Get500ErrorResult(saveError);

        return this.Ok(new AuthResponse
        {
            Token = newAccessToken,
            RefreshToken = newRefreshToken,
            UserId = user.Id,
            Username = user.Username,
        });
    }

    /// <summary>
    /// 根据用户信息生成JWT。
    /// </summary>
    private string GenerateJwtToken(User user)
    {
        string jwtKey = this.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("未配置JWT密钥 (Jwt:Key)。");
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            // 'sub' (Subject) 是JWT标准中用于存放用户唯一标识符的声明。
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),

            // 'nameid' (Name Identifier) 是ClaimTypes中定义的、被ASP.NET Core身份验证系统默认用来查找用户ID的类型。
            // 同时提供这两个声明可以获得最佳的兼容性。
            new Claim(ClaimTypes.NameIdentifier, user.Id),

            // 'name' (Name) 通常用于存放方便阅读的用户名。
            new Claim(JwtRegisteredClaimNames.Name, user.Username),

            // 'jti' (JWT ID) 是一个唯一的令牌ID，可以用于防止重放攻击或用于令牌吊销列表。
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = this.Configuration["Jwt:Issuer"],
            Audience = this.Configuration["Jwt:Audience"],
            Expires = DateTime.UtcNow.AddHours(AccessTokenExpiryHours), // 使用 UtcNow 更标准
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// 生成一个安全的、随机的刷新令牌。
    /// </summary>
    /// <returns>一个 Base64 编码的随机字符串。</returns>
    private string GenerateRefreshToken()
    {
        // 创建一个足够长的字节数组来保证随机性
        byte[] randomNumber = new byte[64];

        // 使用加密服务提供程序 (CSP) 生成高质量的随机数
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);

        // 将随机字节转换为 Base64 字符串，使其易于存储和传输
        return Convert.ToBase64String(randomNumber);
    }
}