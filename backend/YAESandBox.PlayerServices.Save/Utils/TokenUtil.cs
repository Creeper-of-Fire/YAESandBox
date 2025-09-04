namespace YAESandBox.PlayerServices.Save.Utils;

internal class TokenUtil
{
    /// <summary>
    /// 根据项目和存档槽位名创建Token字符串。
    /// </summary>
    public static string CreateToken(string project, string slot) => $"{project}/{slot}";
    /// <summary>
    /// 将Token字符串解析为用于存储服务的路径片段。
    /// </summary>
    public static string[] ParseToken(string? path) =>
        path?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
}