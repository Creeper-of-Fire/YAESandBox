﻿using YAESandBox.Core.Block;
using YAESandBox.Core.DTOs.WebSocket;
using YAESandBox.Depend;

#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释

namespace YAESandBox.Core.DTOs;

/// <summary>
/// 辅助类
/// </summary>
public static class MappingUtility
{
    // 定义一个静态只读字典作为“表”
    // Key: 字段名 (小写)
    // Value: 一个函数，接收当前的 DTO 和 Block，返回应用了该字段更新后的新 DTO
    private static readonly IReadOnlyDictionary<BlockDetailFields, Func<BlockDetailDto, Block.Block, BlockDetailDto>> FieldUpdaters =
        new Dictionary<BlockDetailFields, Func<BlockDetailDto, Block.Block, BlockDetailDto>>
        {
            { BlockDetailFields.Content, (dto, block) => dto with { BlockContent = block.BlockContent } },
            { BlockDetailFields.Metadata, (dto, block) => dto with { Metadata = new Dictionary<string, string>(block.Metadata) } },
        };

    /// <summary>
    /// 创建一个包含指定字段的 BlockDetailDto
    /// </summary>
    /// <param name="block"></param>
    /// <param name="fields"></param>
    /// <returns></returns>
    public static BlockDetailDto CreatePartial(this Block.Block block, params BlockDetailFields[] fields)
    {
        // 初始 DTO 只包含 BlockId
        var currentDto = new BlockDetailDto { BlockId = block.BlockId };

        // 遍历请求包含的字段名
        foreach (var fieldEnum in fields)
        {
            if (!FieldUpdaters.TryGetValue(fieldEnum, out var updaterFunc))
            {
                Log.Warning($"CreatePartialBlockDetailDto: 请求了未知的字段名 '{fieldEnum}'，已忽略。");
                continue;
            }

            currentDto = updaterFunc(currentDto, block);
        }

        // 返回最终构建好的 DTO
        return currentDto;
    }

    /// <summary>
    /// 创建一个包含指定字段的 BlockDetailDto
    /// 不包含BlockStatus特有的那些字段
    /// </summary>
    /// <param name="blockStatus"></param>
    /// <param name="fields"></param>
    /// <returns></returns>
    public static BlockDetailDto CreatePartial(this BlockStatus blockStatus, params BlockDetailFields[] fields)
    {
        return blockStatus.Block.CreatePartial(fields);
    }

    /// <summary>
    /// 将 BlockStatus 转换为 BlockDetailDto
    /// </summary>
    public static BlockDetailDto MapToDetailDto(this BlockStatus blockStatus)
    {
        var block = blockStatus.Block;

        return new BlockDetailDto
        {
            BlockId = block.BlockId,
            StatusCode = blockStatus.StatusCode,
            BlockContent = block.BlockContent,
            Metadata = new Dictionary<string, string>(block.Metadata),
            ConflictDetected = (blockStatus as ConflictBlockStatus)?.MapToConflictDetectedDto(),
            WorkflowInfo = block.MapToWorkflowDto()
        };
    }

    /// <summary>
    /// Block 转换为 WorkflowDto
    /// </summary>
    public static WorkflowDto MapToWorkflowDto(this Block.Block block)
    {
        return new WorkflowDto
        {
            WorkflowName = block.WorkflowName,
            TriggeredParams = block.TriggeredParams,
            TriggeredChildParams = block.TriggeredChildParams,
        };
    }

    /// <summary>
    /// BlockStatus 转换为 ConflictDetectedDto
    /// </summary>
    public static ConflictDetectedDto MapToConflictDetectedDto(this ConflictBlockStatus conflictBlockStatus)
    {
        return new ConflictDetectedDto
        {
            AiCommands = conflictBlockStatus.AiCommands.ToAtomicOperationRequests(),
            UserCommands = conflictBlockStatus.UserCommands.ToAtomicOperationRequests(),
            ConflictingAiCommands = conflictBlockStatus.ConflictingAiCommands.ToAtomicOperationRequests(),
            ConflictingUserCommands = conflictBlockStatus.ConflictingUserCommands.ToAtomicOperationRequests(),
        };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="block"></param>
    /// <returns></returns>
    public static BlockTopologyNodeDto MapToBlockTopologyNodeDto(this IBlockNode block)
    {
        return new BlockTopologyNodeDto
        {
            BlockId = block.BlockId,
            ParentBlockId = block.ParentBlockId,
        };
    }
}

/// <summary>
/// 枚举，用于标识 BlockDetailDto 中包含的字段
/// </summary>
public enum BlockDetailFields
{
    Content,
    Metadata,
}