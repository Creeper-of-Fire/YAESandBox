using YAESandBox.API.DTOs;
using YAESandBox.API.Services.InterFaceAndBasic;
using YAESandBox.Core.Block;
using YAESandBox.Depend;

namespace YAESandBox.API.Services;

/// <summary>
/// 处理 Block 手动管理操作的服务实现。
/// </summary>
public class BlockManagementService(IBlockManager blockManager) : IBlockManagementService
{
    private IBlockManager blockManager { get; } = blockManager;
    
    public async Task<(ManagementResult result, BlockStatus? newBlockStatus)> CreateBlockManuallyAsync(
        string parentBlockId, Dictionary<string, string>? initialMetadata)
    {
        try
        {
            // 参数验证可以在这里做，或者依赖 BlockManager 内部检查
            if (string.IsNullOrWhiteSpace(parentBlockId))
                return (ManagementResult.BadRequest, null);

            // 调用 BlockManager 的内部方法
            var (result, newBlockStatus) = await this.blockManager.InternalCreateBlockManuallyAsync(parentBlockId, initialMetadata);
            
            if (result != ManagementResult.Success || newBlockStatus == null)
                return (result, newBlockStatus);
            Log.Info($"BlockManagementService: 手动创建 Block '{newBlockStatus.Block.BlockId}' 成功。");

            return (result, newBlockStatus);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"BlockManagementService: 手动创建 Block 时发生意外错误 (父: {parentBlockId})。");
            return (ManagementResult.Error, null);
        }
    }

    /// <summary>
    /// 删除一个 Block，并通知相关方
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="recursive"></param>
    /// <param name="force"></param>
    /// <returns></returns>
    public async Task<ManagementResult> DeleteBlockManuallyAsync(string blockId, bool recursive, bool force)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(blockId))
                return ManagementResult.BadRequest;
            
            var status = await this.blockManager.GetBlockAsync(blockId);
            if (status == null)
                return ManagementResult.NotFound;

            var result = await this.blockManager.InternalDeleteBlockManuallyAsync(blockId, recursive, force);

            if (result != ManagementResult.Success)
                return result;
            

            Log.Info($"BlockManagementService: 手动删除 Block '{blockId}' 成功 (recursive={recursive}, force={force})。");

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"BlockManagementService: 手动删除 Block '{blockId}' 时发生意外错误。");
            return ManagementResult.Error;
        }
    }

    public async Task<ManagementResult> MoveBlockManuallyAsync(string blockId, string newParentBlockId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(blockId) || string.IsNullOrWhiteSpace(newParentBlockId))
                return ManagementResult.BadRequest;

            var block = await this.blockManager.GetBlockAsync(blockId);
            if (block == null) // 假设可以直接访问
                return ManagementResult.NotFound;

            var result = await this.blockManager.InternalMoveBlockManuallyAsync(blockId, newParentBlockId);

            if (result == ManagementResult.Success) 
                Log.Info($"BlockManagementService: 手动移动 Block '{blockId}' 到 '{newParentBlockId}' 成功。");

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"BlockManagementService: 手动移动 Block '{blockId}' 到 '{newParentBlockId}' 时发生意外错误。");
            return ManagementResult.Error;
        }
    }
}