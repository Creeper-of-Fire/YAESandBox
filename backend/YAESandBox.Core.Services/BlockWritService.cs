﻿using YAESandBox.Core.Block.BlockManager;
using YAESandBox.Core.DTOs;
using YAESandBox.Core.Services.InterFaceAndBasic;
using YAESandBox.Depend;
using YAESandBox.Depend.Results;

namespace YAESandBox.Core.Services;

/// <summary>
/// BlockWritService 用于管理 Block 的链接和交互。
/// 同时也提供输入原子化指令修改其内容的服务（修改Block数据的唯一入口），之后可能分离。
/// </summary>
public class BlockWritService(IBlockManager blockManager, INotifierService notifierService)
    : BasicBlockService(blockManager, notifierService), IBlockWritService
{
    /// <inheritdoc/>
    public async Task<(BlockResultCode resultCode, BlockStatusCode blockStatusCode)>
        EnqueueOrExecuteAtomicOperationsAsync(string blockId, List<AtomicOperationRequestDto> operations)
    {
        var (atomicOp, blockStatus) =
            await this.BlockManager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations.ToAtomicOperations());
        if (blockStatus == null)
            return (BlockResultCode.NotFound, BlockStatusCode.NotFound);


        bool hasBlockStatusError = false;
        if (atomicOp.TryGetError(out var atomicOpError))
        {
            hasBlockStatusError = atomicOpError is BlockStatusError;
        }

        var resultCode = hasBlockStatusError ? BlockResultCode.Success : BlockResultCode.Error;

        foreach (var error in atomicOp.GetAllItemErrors())
            Log.Error(error.Message); // TODO: 考虑是否要记录Warning信息

        // foreach (var warning in atomicOp.HandledIssue())
        //     Log.Warning(warning.Message);

        await this.NotifierService.NotifyBlockUpdateAsync(blockId, BlockDataFields.WorldState);

        return (resultCode, blockStatus.Value);
    }

    /// <inheritdoc/>
    public async Task<BlockResultCode> UpdateBlockGameStateAsync(
        string blockId, Dictionary<string, object?> settingsToUpdate)
    {
        var code = await this.BlockManager.UpdateBlockGameStateAsync(blockId, settingsToUpdate);
        if (code == BlockResultCode.Success)
            await this.NotifierService.NotifyBlockUpdateAsync(blockId, BlockDataFields.GameState);
        return code;
    }


    /// <inheritdoc/>
    public async Task<BlockResultCode> UpdateBlockDetailsAsync(string blockId, UpdateBlockDetailsDto updateDto)
    {
        // 没有提供任何更新内容，可以认为操作“成功”但无效果，或返回 BadRequest
        if (updateDto.Content == null && (updateDto.MetadataUpdates == null || !updateDto.MetadataUpdates.Any()))
        {
            Log.Debug($"Block '{blockId}': 收到空的更新请求，无操作。");
            return BlockResultCode.InvalidInput;
        }

        var resultCode = await this.BlockManager.UpdateBlockDetailsAsync(blockId, updateDto.Content, updateDto.MetadataUpdates);

        if (updateDto.Content != null)
            await this.NotifierService.NotifyBlockUpdateAsync(blockId, BlockDataFields.BlockContent);
        if (updateDto.MetadataUpdates != null && updateDto.MetadataUpdates.Any())
            await this.NotifierService.NotifyBlockUpdateAsync(blockId, BlockDataFields.Metadata);

        return resultCode;
    }
}