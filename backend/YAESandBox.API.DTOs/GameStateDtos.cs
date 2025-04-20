using System.ComponentModel.DataAnnotations;

namespace YAESandBox.API.DTOs;

/// <summary>
/// 用于 API 响应，表示一个 Block 的 GameState。
/// </summary>
public record GameStateDto
{
    /// <summary>
    /// 包含 GameState 所有设置的字典。
    /// 键是设置的名称 (string)，值是设置的值 (object?)。
    /// 值的实际类型取决于具体的游戏状态设置。
    /// </summary>
    [Required]
    public required Dictionary<string, object?> Settings { get; set; } = new();
}

/// <summary>
/// 用于 API 请求，表示要更新的 GameState 设置。
/// </summary>
public record UpdateGameStateRequestDto
{
    /// <summary>
    /// 一个字典，包含要更新或添加的 GameState 设置。
    /// 键是要修改的设置名称，值是新的设置值。
    /// 如果值为 null，通常表示移除该设置或将其设置为空。
    /// </summary>
    [Required(ErrorMessage = "要更新的设置不能为空")]
    public Dictionary<string, object?> SettingsToUpdate { get; set; } = new();
}