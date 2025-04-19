using Nito.AsyncEx;
using YAESandBox.API.DTOs;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;
using static YAESandBox.Core.Block.BlockTopologyExporter;

namespace YAESandBox.API.Services;

/// <summary>
/// BlockReadService专注于单个 Block 的数据查询服务（主要是其中的worldState和gameState），仅限只读操作。
/// 它也提供对于整体的总结服务。
/// </summary>
/// <param name="notifierService"></param>
/// <param name="blockManager"></param>
public class BlockReadService(INotifierService notifierService, IBlockManager blockManager) :
    BasicBlockService(notifierService, blockManager), IBlockReadService
{
    public async Task<IReadOnlyDictionary<string, BlockDetailDto>> GetAllBlockDetailsAsync()
    {
        var tasks = this.blockManager.GetBlocks().Keys
            .Select(this.GetBlockDetailDtoAsync)
            .ToList();
        var results = await tasks.WhenAll();
        return results.Where(x => x != null).Select(x => x!).ToDictionary(x => x.BlockId, x => x);
    }

    public Task<JsonBlockNode?> GetBlockTopologyJsonAsync()
    {
        return Task.FromResult(GenerateTopologyJson(this.blockManager.GetNodeOnlyBlocks()));
    }

    public async Task<BlockDetailDto?> GetBlockDetailDtoAsync(string blockId)
    {
        var block = await this.GetBlockAsync(blockId);
        return block == null ? null : this.MapToDetailDto(block);
    }

    public async Task<GameState?> GetBlockGameStateAsync(string blockId)
    {
        var block = await this.GetBlockAsync(blockId);
        return block?.Block.GameState;
    }

    public async Task<IEnumerable<BaseEntity>?> GetAllEntitiesSummaryAsync(string blockId)
    {
        var block = await this.GetBlockAsync(blockId);
        if (block == null) return null;
        try
        {
            var targetWs = block.CurrentWorldState;
            // Return all non-destroyed entities
            return targetWs.Items.Values.Cast<BaseEntity>()
                .Concat(targetWs.Characters.Values)
                .Concat(targetWs.Places.Values)
                .Where(e => !e.IsDestroyed)
                .ToList(); // Create a copy
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Block '{blockId}': 获取实体摘要时出错: {ex.Message}");
            return null;
        }
    }

    public async Task<BaseEntity?> GetEntityDetailAsync(string blockId, TypedID entityRef)
    {
        var block = await this.GetBlockAsync(blockId);
        if (block == null) return null;

        try
        {
            var targetWs = block.CurrentWorldState;
            return targetWs.FindEntity(entityRef, includeDestroyed: false); // Find non-destroyed
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Block '{blockId}': 获取实体 '{entityRef}' 详情时出错: {ex.Message}");
            return null;
        }
    }

    // --- DTO Mapping Helpers ---
    private BlockDetailDto MapToDetailDto(BlockStatus block)
    {
        return new BlockDetailDto
        {
            BlockId = block.Block.BlockId,
            ParentBlockId = block.Block.ParentBlockId,
            StatusCode = block.StatusCode,
            BlockContent = block.Block.BlockContent,
            Metadata = new Dictionary<string, string>(block.Block.Metadata), // Return copy
            ChildrenInfo = new List<string>(block.Block.ChildrenList) // Return copy
        };
    }
}