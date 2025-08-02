using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using YAESandBox.Depend.AspNetCore;
namespace YAESandBox.Authentication;

/// <summary>
/// 登录请求的数据模型。
/// </summary>
public record LoginRequest(
    [Required(ErrorMessage = "用户名不能为空")] string Username,
    [Required(ErrorMessage = "密码不能为空")] string Password
);

/// <summary>
/// 注册请求的数据模型。
/// </summary>
public record RegisterRequest(
    [Required(ErrorMessage = "用户名不能为空")] string Username,
    [Required(ErrorMessage = "密码不能为空")] string Password
);

/// <summary>
/// 成功认证后的响应数据模型。
/// </summary>
public record AuthResponse(string Token, string UserId, string Username);

/// <summary>
/// 提供用户注册和登录的端点。
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[ApiExplorerSettings(GroupName = AuthenticationModule.AuthenticationGroupName)]
public class AuthController(UserService userService, IConfiguration configuration) : ControllerBase
{
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
    /// 用户登录并获取JWT。
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
        
        string token = this.GenerateJwtToken(user);

        return this.Ok(new AuthResponse(token, user.Id, user.Username));
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
            Expires = DateTime.UtcNow.AddHours(8), // 使用 UtcNow 更标准
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}