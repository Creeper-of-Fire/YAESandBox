using System.ComponentModel.DataAnnotations;
using YAESandBox.Core.Block;
using YAESandBox.Core.State; // For BlockStatusCode

namespace YAESandBox.API.DTOs;

/// <summary>
/// 用于获取单个 Block 详细信息的响应 (不含 WorldState)。
/// </summary>
public record BlockDetailDto
{
    public string BlockId { get; init; } = null!;
    public string? ParentBlockId { get; init; }
    public BlockStatusCode StatusCode { get; init; }
    public string BlockContent { get; init; } = string.Empty;
    public Dictionary<string, string> Metadata { get; init; } = new();
    public List<string> ChildrenInfo { get; init; } = new();
    // 注意：WsInput, WsPostAI, WsPostUser, WsTemp 不应直接通过 API 暴露
}