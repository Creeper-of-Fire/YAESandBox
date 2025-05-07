using YAESandBox.API.DTOs;
using YAESandBox.Depend;

namespace YAESandBox.API.Services.InterFaceAndBasic;

public interface IBlockWritService
{
    /// <summary>
    /// 更新 Block 的GameState
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="settingsToUpdate"></param>
    /// <returns></returns>
    Task<BlockResultCode> UpdateBlockGameStateAsync(string blockId, Dictionary<string, object?> settingsToUpdate);

    /// <summary>
    /// 输入原子化指令修改其内容
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="operations"></param>
    /// <returns></returns>
    Task<(BlockResultCode resultCode, BlockStatusCode blockStatusCode)> EnqueueOrExecuteAtomicOperationsAsync(string blockId,
        List<AtomicOperationRequestDto> operations);


    /// <summary>
    /// 部分更新指定 Block 的内容和/或元数据。
    /// 仅在 Block 处于 Idle 状态时允许操作。
    /// </summary>
    /// <param name="blockId">要更新的 Block ID。</param>
    /// <param name="updateDto">包含要更新内容的 DTO。</param>
    /// <returns>更新操作的结果。</returns>
    Task<BlockResultCode> UpdateBlockDetailsAsync(string blockId, UpdateBlockDetailsDto updateDto);
}