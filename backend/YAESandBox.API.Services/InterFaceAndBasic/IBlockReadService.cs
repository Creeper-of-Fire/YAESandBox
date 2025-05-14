using YAESandBox.API.DTOs;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;

namespace YAESandBox.API.Services.InterFaceAndBasic;

public interface IBlockReadService
{
    Task<IReadOnlyDictionary<string, BlockDetailDto>> GetAllBlockDetailsAsync();
    Task<List<BlockTopologyNodeDto>> GetBlockTopologyListAsync(string? blockId);
    Task<BlockDetailDto?> GetBlockDetailDtoAsync(string blockId);
    Task<GameState?> GetBlockGameStateAsync(string blockId);
    Task<IEnumerable<BaseEntity>?> GetAllEntitiesSummaryAsync(string blockId); // 返回 Core 对象供 Controller 映射
    Task<BaseEntity?> GetEntityDetailAsync(string blockId, TypedId entityRef); // 返回 Core 对象供 Controller 映射
}