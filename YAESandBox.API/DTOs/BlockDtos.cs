using System.ComponentModel.DataAnnotations;
using YAESandBox.Core.Block;
using YAESandBox.Core.State; // For BlockStatusCode

namespace YAESandBox.API.DTOs;

/// <summary>
/// 用于 API 响应，表示单个 Block 的详细信息（不包含 WorldState）。
/// </summary>
public record BlockDetailDto
{
    /// <summary>
    /// Block 的唯一标识符。
    /// </summary>
    public string BlockId { get; init; } = null!;

    /// <summary>
    /// 父 Block 的 ID。如果为根节点，则为 null。
    /// </summary>
    public string? ParentBlockId { get; init; }

    /// <summary>
    /// Block 当前的状态码 (例如 Idle, Loading, ResolvingConflict, Error)。
    /// </summary>
    public BlockStatusCode StatusCode { get; init; }

    /// <summary>
    /// Block 的主要文本内容 (例如 AI 生成的文本、配置等)。
    /// </summary>
    public string BlockContent { get; init; } = string.Empty;

    /// <summary>
    /// 与 Block 相关的元数据字典 (键值对均为字符串)。
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// 该 Block 的直接子 Block 的 ID 列表。
    /// </summary>
    public List<string> ChildrenInfo { get; init; } = new();
    // 注意：WsInput, WsPostAI, WsPostUser, WsTemp 不应直接通过 API 暴露
}