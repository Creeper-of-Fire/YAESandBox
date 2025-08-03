using YAESandBox.Seed.Block;
using YAESandBox.Seed.Block.BlockManager;
using YAESandBox.Seed.DTOs;
using YAESandBox.Seed.Services.InterFaceAndBasic;
using YAESandBox.Depend;

namespace YAESandBox.Seed.Services;

/// <summary>
/// 处理 Block 手动管理操作的服务实现。
/// 和其他Service不同，它必须依赖 INotifierService ，因为只有它有足够的上下文 
/// </summary>
public class BlockManagementService(IBlockManager blockManager, INotifierService notifierService)
    : IBlockManagementService
{
    private IBlockManager BlockManager { get; } = blockManager;
    private INotifierService NotifierService { get; } = notifierService;

    /// <summary>
    /// 创建一个 Block，并通知相关方
    /// </summary>
    /// <param name="parentBlockId"></param>
    /// <param name="initialMetadata"></param>
    /// <returns></returns>
    public async Task<(ManagementResult result, BlockStatus? newBlockStatus)> CreateBlockManuallyAsync(
        string parentBlockId, Dictionary<string, string>? initialMetadata)
    {
        try
        {
            // 参数验证可以在这里做，或者依赖 BlockManager 内部检查
            if (string.IsNullOrWhiteSpace(parentBlockId))
                return (ManagementResult.BadRequest, null);

            // 调用 BlockManager 的内部方法
            var (result, newBlockStatus) =
                await this.BlockManager.InternalCreateBlockManuallyAsync(parentBlockId, initialMetadata);

            // 操作成功后，可以选择性地发送通知
            if (result != ManagementResult.Success || newBlockStatus == null)
                return (result, newBlockStatus);
            // 通知新 Block 创建
            await this.NotifierService.NotifyBlockStatusUpdateAsync(newBlockStatus.Block.BlockId, newBlockStatus.StatusCode);
            // 通知父 Block 可能的结构变化 (如果前端需要知道 ChildrenList 更新)
            // await notifierService.NotifyBlockStructureUpdateAsync(parentBlockId); // 需要定义新的通知类型
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
            if (string.IsNullOrWhiteSpace(blockId)) return ManagementResult.BadRequest;

            // 查找旧父ID，以便删除后通知
            string? oldParentId = null;
            var status = await this.BlockManager.GetBlockAsync(blockId);
            if (status != null) // 假设可以直接访问
            {
                oldParentId = status.Block.ParentBlockId;
            }

            var result = await this.BlockManager.InternalDeleteBlockManuallyAsync(blockId, recursive, force);

            if (result != ManagementResult.Success)
                return result;
            // TODO 换成其他的更新方式或者不更新
            // await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, BlockStatusCode.Deleted);

            // 通知父 Block 结构变化
            if (oldParentId != null)
                await this.NotifierService.NotifyBlockUpdateAsync(oldParentId, BlockDataFields.ChildrenInfo);

            Log.Info($"BlockManagementService: 手动删除 Block '{blockId}' 成功 (recursive={recursive}, force={force})。");

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"BlockManagementService: 手动删除 Block '{blockId}' 时发生意外错误。");
            return ManagementResult.Error;
        }
    }

    /// <summary>
    /// 移动一个 Block，并通知相关方
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="newParentBlockId"></param>
    /// <returns></returns>
    public async Task<ManagementResult> MoveBlockManuallyAsync(string blockId, string newParentBlockId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(blockId) || string.IsNullOrWhiteSpace(newParentBlockId))
                return ManagementResult.BadRequest;

            // 查找旧父ID
            string? oldParentId = null;
            var status = await this.BlockManager.GetBlockAsync(blockId);
            if (status != null) // 假设可以直接访问
                oldParentId = status.Block.ParentBlockId;

            var result = await this.BlockManager.InternalMoveBlockManuallyAsync(blockId, newParentBlockId);

            if (result != ManagementResult.Success)
                return result;
            await this.NotifierService.NotifyBlockUpdateAsync(blockId, BlockDataFields.ParentBlockId);

            // 通知旧父节点结构变化
            if (oldParentId != null && oldParentId != newParentBlockId)
            {
                await this.NotifierService.NotifyBlockUpdateAsync(oldParentId, BlockDataFields.ChildrenInfo);
            }

            // 通知新父节点结构变化
            // await notifierService.NotifyBlockStructureUpdateAsync(newParentBlockId);
            await this.NotifierService.NotifyBlockUpdateAsync(newParentBlockId, BlockDataFields.ChildrenInfo);

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