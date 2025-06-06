// --- START OF FILE IBlockManagementService.cs ---

// For potential DTOs used/returned by service

using YAESandBox.Core.Block;
using YAESandBox.Depend;

// For BlockStatus, Result Enums

namespace YAESandBox.Core.Services.InterFaceAndBasic;

/// <summary>
/// 提供 Block 手动管理操作的服务接口。
/// </summary>
public interface IBlockManagementService
{
    /// <summary>
    /// 手动创建一个新的、空的 Block。
    /// </summary>
    /// <param name="parentBlockId">父 Block ID。</param>
    /// <param name="initialMetadata">(可选) 初始元数据。</param>
    /// <returns>操作结果，成功时包含新创建的 BlockStatus (Idle)。</returns>
    Task<(ManagementResult result, BlockStatus? newBlockStatus)> CreateBlockManuallyAsync(string parentBlockId,
        Dictionary<string, string>? initialMetadata);

    /// <summary>
    /// 手动删除一个指定的 Block。
    /// </summary>
    /// <param name="blockId">要删除的 Block ID。</param>
    /// <param name="recursive">是否递归删除子 Block。</param>
    /// <param name="force">是否强制删除，无视状态。</param>
    /// <returns>删除操作的结果。</returns>
    Task<ManagementResult> DeleteBlockManuallyAsync(string blockId, bool recursive, bool force);

    /// <summary>
    /// 手动将一个 Block（及其子树）移动到另一个父 Block 下。
    /// </summary>
    /// <param name="blockId">要移动的 Block ID。</param>
    /// <param name="newParentBlockId">新的父 Block ID。</param>
    /// <returns>移动操作的结果。</returns>
    Task<ManagementResult> MoveBlockManuallyAsync(string blockId, string newParentBlockId);
}