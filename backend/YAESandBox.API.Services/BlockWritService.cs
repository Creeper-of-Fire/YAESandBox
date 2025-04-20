using FluentResults;
using YAESandBox.API.DTOs;
using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.API.Services.InterFaceAndBasic;
using YAESandBox.Core;
using YAESandBox.Core.Action;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Depend;

namespace YAESandBox.API.Services;

/// <summary>
/// BlockWritService 用于管理 Block 的链接和交互。
/// 同时也提供输入原子化指令修改其内容的服务（修改Block数据的唯一入口），之后可能分离。
/// </summary>
/// <param name="blockManager"></param>
public class BlockWritService(IBlockManager blockManager) : BasicBlockService(blockManager), IBlockWritService
{
    /// <inheritdoc/>
    public async Task<(BlockResultCode resultCode, BlockStatusCode blockStatusCode)>
        EnqueueOrExecuteAtomicOperationsAsync(string blockId, List<AtomicOperationRequestDto> operations)
    {
        var (atomicOp, blockStatus) =
            await this.blockManager.EnqueueOrExecuteAtomicOperationsAsync(blockId, operations.ToAtomicOperations());
        if (blockStatus == null)
            return (BlockResultCode.NotFound, BlockStatusCode.NotFound);

        bool hasBlockStatusError = atomicOp.HasError<BlockStatusError>();
        var resultCode = hasBlockStatusError ? BlockResultCode.Success : BlockResultCode.Error;

        foreach (var error in atomicOp.Errors)
            Log.Error(error.Message);

        foreach (var warning in atomicOp.HandledIssue())
            Log.Warning(warning.Message);

        if (hasBlockStatusError)
            return (resultCode, blockStatus.Value);

        return (resultCode, blockStatus.Value);
    }

    /// <inheritdoc/>
    public async Task<UpdateResult> UpdateBlockGameStateAsync(
        string blockId, Dictionary<string, object?> settingsToUpdate)
        => await this.blockManager.UpdateBlockGameStateAsync(blockId, settingsToUpdate);


    /// <inheritdoc/>
    public async Task<BlockResultCode> UpdateBlockDetailsAsync(string blockId, UpdateBlockDetailsDto updateDto)
    {
        // 参数验证可以在这里做，或者委托给 Manager
        if (updateDto.Content != null || updateDto.MetadataUpdates != null)
            return await this.blockManager.UpdateBlockDetailsAsync(blockId, updateDto.Content, updateDto.MetadataUpdates);

        // 没有提供任何更新内容，可以认为操作“成功”但无效果，或返回 BadRequest
        // 为了简单，我们认为这是一个无操作的成功
        Log.Debug($"Block '{blockId}': 收到空的更新请求，无操作。");
        return BlockResultCode.InvalidInput;
    }
}