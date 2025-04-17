using YAESandBox.Core.Block;
using YAESandBox.Depend;

namespace YAESandBox.API.Services;

/// <summary>
/// 处理 Block 手动管理操作的服务实现。
/// </summary>
public class BlockManagementService(IBlockManager blockManager, INotifierService notifierService)
    : IBlockManagementService
{
    private IBlockManager blockManager { get; } = blockManager;
    private INotifierService notifierService { get; } = notifierService;


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
                await this.blockManager.InternalCreateBlockManuallyAsync(parentBlockId, initialMetadata);

            // 操作成功后，可以选择性地发送通知
            if (result != ManagementResult.Success || newBlockStatus == null)
                return (result, newBlockStatus);
            // 通知新 Block 创建
            await this.notifierService.NotifyBlockStatusUpdateAsync(newBlockStatus.Block.BlockId,
                newBlockStatus.StatusCode);
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

    public async Task<ManagementResult> DeleteBlockManuallyAsync(string blockId, bool recursive, bool force)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(blockId)) return ManagementResult.BadRequest;

            // 查找旧父ID，以便删除后通知
            string? oldParentId = null;
            var status = await this.blockManager.GetBlockAsync(blockId);
            if (status != null) // 假设可以直接访问
            {
                oldParentId = status.Block.ParentBlockId;
            }

            var result = await this.blockManager.InternalDeleteBlockManuallyAsync(blockId, recursive, force);

            if (result == ManagementResult.Success)
            {
                // 通知 Block 被删除 (前端可能需要特殊处理或移除)
                // 可以发送一个特定的 "BlockDeleted" 消息，或发送一个特殊的状态码？
                // 或者依赖前端在下次获取列表时发现它消失了。
                // 这里我们发送一个状态更新（虽然它已被删除）让前端知道发生了事情
                await this.notifierService.NotifyBlockStatusUpdateAsync(blockId,
                    BlockStatusCode.Idle); // 或者一个虚构的 Deleted 状态?

                // 通知父 Block 结构变化
                if (oldParentId != null)
                {
                    // await notifierService.NotifyBlockStructureUpdateAsync(oldParentId);
                    // 或者发送一个 StateUpdateSignal 给父节点
                    await this.notifierService.NotifyStateUpdateAsync(oldParentId);
                }

                Log.Info($"BlockManagementService: 手动删除 Block '{blockId}' 成功 (recursive={recursive}, force={force})。");
            }

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

            // 查找旧父ID
            string? oldParentId = null;
            var status = await this.blockManager.GetBlockAsync(blockId);
            if (status != null) // 假设可以直接访问
            {
                oldParentId = status.Block.ParentBlockId;
            }

            var result = await this.blockManager.InternalMoveBlockManuallyAsync(blockId, newParentBlockId);

            if (result == ManagementResult.Success)
            {
                // 通知被移动的 Block (可能更新其显示路径或父节点信息)
                await this.notifierService.NotifyBlockStatusUpdateAsync(blockId, BlockStatusCode.Idle); // 状态没变，但结构变了

                // 通知旧父节点结构变化
                if (oldParentId != null && oldParentId != newParentBlockId) // 避免自己移动到自己下面（虽然已被阻止）或父节点没变
                {
                    // await notifierService.NotifyBlockStructureUpdateAsync(oldParentId);
                    await this.notifierService.NotifyStateUpdateAsync(oldParentId);
                }

                // 通知新父节点结构变化
                // await notifierService.NotifyBlockStructureUpdateAsync(newParentBlockId);
                await this.notifierService.NotifyStateUpdateAsync(newParentBlockId);

                Log.Info($"BlockManagementService: 手动移动 Block '{blockId}' 到 '{newParentBlockId}' 成功。");
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"BlockManagementService: 手动移动 Block '{blockId}' 到 '{newParentBlockId}' 时发生意外错误。");
            return ManagementResult.Error;
        }
    }
}