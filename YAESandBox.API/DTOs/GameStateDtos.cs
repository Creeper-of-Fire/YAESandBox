using System.ComponentModel.DataAnnotations;

namespace YAESandBox.API.DTOs;

/// <summary>
/// 用于获取 gameState 的响应。
/// </summary>
public class GameStateDto
{
    public Dictionary<string, object?> Settings { get; set; } = new();
}

/// <summary>
/// 用于修改 gameState 的请求体。
/// </summary>
public class UpdateGameStateRequestDto
{
    [Required]
    public Dictionary<string, object?> SettingsToUpdate { get; set; } = new();
}