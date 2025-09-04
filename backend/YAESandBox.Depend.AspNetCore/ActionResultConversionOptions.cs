using Microsoft.AspNetCore.Http;

namespace YAESandBox.Depend.AspNetCore;

/// <summary>
/// 为 Result 到 ActionResult 的转换提供配置选项。
/// </summary>
/// <param name="SuccessStatusCode">当 Result 成功时，要返回的 HTTP 状态码。如果为 null，则使用默认值（200 OK 或 204 No Content）。</param>
public sealed record ActionResultConversionOptions(int? SuccessStatusCode = null)
{
    /// <summary>
    /// 表示默认选项，成功时返回 200 OK 或 204 No Content。
    /// </summary>
    public static ActionResultConversionOptions Default { get; } = new();

    /// <summary>
    ///   表示创建操作的默认选项，成功时返回 201 Created。
    ///   用法: `result.ToActionResult(ActionResultConversionOptions.Created)`
    ///   </summary>
    public static ActionResultConversionOptions Created { get; } = new(StatusCodes.Status201Created);

    /// <summary>
    ///   表示接受操作的默认选项，成功时返回 202 Accepted。
    ///   用法: `result.ToActionResult(ActionResultConversionOptions.Accepted)`
    ///   </summary>
    public static ActionResultConversionOptions Accepted { get; } = new(StatusCodes.Status202Accepted);

// 未来可以轻松扩展，例如:
// public string? LocationHeader { get; init; }
}