using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YAESandBox.Authentication;

/// <summary>
/// 为需要身份验证的 API 控制器提供一个通用基类。
/// 自动应用 [Authorize] 和 [ApiController] 特性，并提供一个方便的 UserId 属性。
/// </summary>
[ApiController] // 1. 所有派生类都是 API 控制器
[Authorize] // 2. 所有派生类的端点默认都需要授权
public abstract class AuthenticatedApiControllerBase : ControllerBase
{
    /// <summary>
    /// 获取当前已认证用户的唯一 ID。
    /// 该 ID 从 JWT 的 'sub' 或 'nameidentifier' 声明中解析。
    /// 如果无法解析，将抛出 UnauthorizedAccessException。
    /// </summary>
    protected string UserId => this.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                               throw new UnauthorizedAccessException("无法从 Token 中解析用户 ID。");
}