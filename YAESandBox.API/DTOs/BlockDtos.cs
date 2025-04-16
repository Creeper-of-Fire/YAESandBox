using System.ComponentModel.DataAnnotations;
using YAESandBox.Core.Block;
using YAESandBox.Core.State; // For BlockStatusCode

namespace YAESandBox.API.DTOs;

/// <summary>
/// 用于 Block 列表的摘要信息。
/// </summary>
public class BlockSummaryDto
{
    public string BlockId { get; set; } = null!;
    public string? ParentBlockId { get; set; }
    public BlockStatusCode StatusCode { get; set; }
    public int SelectedChildIndex { get; set; }
    public string? ContentSummary { get; set; } // Block 内容的摘要或标题
    public DateTime CreationTime { get; set; } // 从 Metadata 获取
}

/// <summary>
/// 用于获取单个 Block 详细信息的响应 (不含 WorldState)。
/// </summary>
public class BlockDetailDto : BlockSummaryDto
{
    public string BlockContent { get; set; } = string.Empty;
    public Dictionary<string, object?> Metadata { get; set; } = new();
    public Dictionary<int, string> ChildrenInfo { get; set; } = new();
    // 注意：WsInput, WsPostAI, WsPostUser, WsTemp 不应直接通过 API 暴露
}

/// <summary>
/// 用于更新父 Block 选择的子节点索引的请求。
/// </summary>
public class SelectChildRequestDto
{
    [Required]
    public int SelectedChildIndex { get; set; }
}