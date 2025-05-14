using Nito.AsyncEx;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services.InterFaceAndBasic;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend;
using static YAESandBox.API.Services.BlockTopologyExporter;

namespace YAESandBox.API.Services;

/// <summary>
/// BlockReadService专注于单个 Block 的数据查询服务（主要是其中的worldState和gameState），仅限只读操作。
/// 它也提供对于整体的总结服务。
/// </summary>
public class BlockReadService(IBlockManager blockManager, INotifierService notifierService)
    : BasicBlockService(blockManager, notifierService), IBlockReadService
{
    ///<inheritdoc/>
    public async Task<IReadOnlyDictionary<string, BlockDetailDto>> GetAllBlockDetailsAsync()
    {
        var tasks = this.BlockManager.GetBlocks().Keys
            .Select(this.GetBlockDetailDtoAsync)
            .ToList();
        var results = await tasks.WhenAll();
        return results.Where(x => x != null).Select(x => x!).ToDictionary(x => x.BlockId, x => x);
    }

    ///<inheritdoc/>
    public Task<List<BlockTopologyNodeDto>> GetBlockTopologyListAsync(string? blockId)
    {
        var result = GenerateTopologyList(this.BlockManager.GetNodeOnlyBlocks(), blockId ?? Core.Block.BlockManager.WorldRootId);
        foreach (var e in result.Errors)
            Log.Error(e.Message);
        if (result.TryGetValue(out var dtos))
            return Task.FromResult(dtos);
        return Task.FromResult<List<BlockTopologyNodeDto>>([]);
    }

    ///<inheritdoc/>
    public async Task<BlockDetailDto?> GetBlockDetailDtoAsync(string blockId)
    {
        var block = await this.GetBlockAsync(blockId);
        return block?.MapToDetailDto();
    }

    ///<inheritdoc/>
    public async Task<GameState?> GetBlockGameStateAsync(string blockId)
    {
        var block = await this.GetBlockAsync(blockId);
        return block?.Block.GameState;
    }

    ///<inheritdoc/>
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

    ///<inheritdoc/>
    public async Task<BaseEntity?> GetEntityDetailAsync(string blockId, TypedId entityRef)
    {
        var block = await this.GetBlockAsync(blockId);
        if (block == null) return null;

        try
        {
            var targetWs = block.CurrentWorldState;
            return targetWs.FindEntity(entityRef, false); // Find non-destroyed
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Block '{blockId}': 获取实体 '{entityRef}' 详情时出错: {ex.Message}");
            return null;
        }
    }
}