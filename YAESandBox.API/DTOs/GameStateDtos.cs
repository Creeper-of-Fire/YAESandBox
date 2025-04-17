using System.ComponentModel.DataAnnotations;

namespace YAESandBox.API.DTOs;

/// <summary>
/// 用于获取 GameState 的响应。
/// </summary>
public record GameStateDto
{
    public Dictionary<string, object?> Settings { get; set; } = new();
}

/// <summary>
/// 用于修改 GameState 的请求体。
/// </summary>
public record UpdateGameStateRequestDto
{
    [Required]
    public Dictionary<string, object?> SettingsToUpdate { get; set; } = new();
}