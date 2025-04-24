using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FluentResults;
using YAESandBox.API.DTOs;
using YAESandBox.Core;
using YAESandBox.Core.Block;
using YAESandBox.Depend;

namespace YAESandBox.API.Services;

public static class BlockTopologyExporter
{
    /// <summary>
    /// 用于序列化为 JSON 的内部节点表示。
    /// </summary>
    public record JsonBlockNode(string Id)
    {
        /// <summary>
        /// 节点 ID。
        /// </summary>
        [Required]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Id;

        /// <summary>
        /// 子节点列表。
        /// </summary>
        [Required]
        [JsonPropertyName("children")]
        public List<JsonBlockNode> Children { get; } = new();
    }

    /// <summary>
    /// 从 Block 字典生成表示拓扑结构的扁平节点列表。
    /// 每个节点包含其 ID 和父节点 ID。
    /// </summary>
    /// <param name="allBlocks">包含所有 Block 节点的只读字典 (BlockId -> IBlockNode)。</param>
    /// <param name="rootId">
    /// （可选）要开始遍历的根节点 ID。
    /// 如果提供，则只包含以此节点为根的子树（包含自身）中的节点。
    /// 如果为 null 或空，则包含从最高根节点开始的所有节点。
    /// </param>
    /// <returns>
    /// 包含 BlockTopologyNodeDto 的列表。
    /// 如果指定的 rootId 无效，或者发生内部错误，则返回包含错误的 Result。
    /// 如果成功但没有找到任何节点（例如 allBlocks 为空），则返回包含空列表的成功 Result。
    /// </returns>
    public static Result<List<BlockTopologyNodeDto>> GenerateTopologyList(
        IReadOnlyDictionary<string, IBlockNode> allBlocks,
        string? rootId = null // 改为可选参数
    )
    {
        if (!allBlocks.Any())
        {
            Log.Warning("GenerateFlatTopologyList: 输入的 Block 字典为空或为 null。");
            // 返回空的成功列表是合理的，表示没有节点
            return new List<BlockTopologyNodeDto>();
        }

        // 确定实际的起始节点 ID
        string startNodeId = string.IsNullOrEmpty(rootId) ? BlockManager.WorldRootId : rootId;

        // 验证起始节点是否存在
        if (!allBlocks.ContainsKey(startNodeId))
        {
            return NormalError.NotFound($"GenerateFlatTopologyList: 指定的起始节点 ID '{startNodeId}' 在字典中不存在。")
                .ToResult<List<BlockTopologyNodeDto>>();
        }

        var flatList = new List<BlockTopologyNodeDto>();
        var visited = new HashSet<string>(); // 用于防止无限循环（理论上树结构不应有环）
        var queue = new Queue<string>(); // 使用队列进行广度优先或深度优先遍历

        queue.Enqueue(startNodeId);
        visited.Add(startNodeId);

        try
        {
            // 可以使用 BFS 或 DFS 遍历子树
            while (queue.Count > 0)
            {
                string currentBlockId = queue.Dequeue();

                if (!allBlocks.TryGetValue(currentBlockId, out var currentNode))
                {
                    // 在遍历过程中发现节点丢失，记录警告但继续处理其他节点
                    Log.Warning($"GenerateFlatTopologyList: 遍历时节点 ID '{currentBlockId}' 意外丢失。");
                    continue;
                }

                // 创建 DTO 并添加到列表
                flatList.Add(new BlockTopologyNodeDto
                {
                    BlockId = currentNode.BlockId,
                    ParentBlockId = currentNode.ParentBlockId // 直接从 IBlockNode 获取父 ID
                    // 可以按需添加其他摘要信息
                });

                // 将子节点加入队列进行处理
                foreach (string childId in currentNode.ChildrenList)
                {
                    // 检查子节点是否存在于字典中，并且未被访问过
                    if (allBlocks.ContainsKey(childId) && visited.Add(childId)) // visited.Add 返回 true 如果添加成功 (之前不存在)
                    {
                        queue.Enqueue(childId);
                    }
                    else if (!allBlocks.ContainsKey(childId))
                    {
                        Log.Warning($"GenerateFlatTopologyList: 节点 '{currentBlockId}' 的子节点 ID '{childId}' 在字典中不存在。");
                    }
                    // 如果 visited.Add 返回 false，说明遇到了环，此处会跳过，避免无限循环
                    else if (!visited.Contains(childId)) // Sanity check, visited.Add 应该已经处理了环
                    {
                        Log.Error($"GenerateFlatTopologyList: 检测到可能的环或逻辑错误，节点 '{childId}' 已被访问但 visited.Add 返回 false?");
                    }
                }
            }

            // 成功生成列表
            return flatList;
        }
        catch (Exception ex)
        {
            // 捕获遍历或处理过程中发生的任何异常
            Log.Error(ex, "GenerateFlatTopologyList: 生成扁平拓扑列表时发生错误。");
            return NormalError.Error("生成扁平拓扑列表时发生内部错误。").ToResult<List<BlockTopologyNodeDto>>();
        }
    }
}