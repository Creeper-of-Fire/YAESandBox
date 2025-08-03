using YAESandBox.Seed.State;
using YAESandBox.Seed.State.Entity;
using YAESandBox.Seed.Workflow.CoreModules;

namespace YAESandBox.Workflow.Test;

/// <summary>
/// IWorkflowDataAccess 的模拟实现，用于控制台环境。
/// 由于没有连接到真实的游戏状态，它只返回默认或空值。
/// </summary>
public class MockWorkflowDataAccess : IWorkflowGameDataAccess
{
    /// <inheritdoc />
    public Task<GameState?> GetGameStateAsync(string blockId)
    {
        Console.WriteLine($"[数据访问模拟] 请求获取 Block '{blockId}' 的 GameState (返回 null)。");
        return Task.FromResult<GameState?>(null);
    }

    /// <inheritdoc />
    public Task<BaseEntity?> GetEntityAsync(string blockId, TypedId entityId, bool includeDestroyed = false)
    {
        Console.WriteLine($"[数据访问模拟] 请求获取 Block '{blockId}' 中实体 '{entityId}' (返回 null)。");
        return Task.FromResult<BaseEntity?>(null);
    }

    /// <inheritdoc />
    public Task<IEnumerable<BaseEntity>?> GetAllEntitiesAsync(string blockId, bool includeDestroyed = false)
    {
        Console.WriteLine($"[数据访问模拟] 请求获取 Block '{blockId}' 的所有实体 (返回空列表)。");
        return Task.FromResult<IEnumerable<BaseEntity>?>([]);
    }

    /// <inheritdoc />
    public Task<string?> GetBlockRawTextAsync(string blockId)
    {
        Console.WriteLine($"[数据访问模拟] 请求获取 Block '{blockId}' 的 RawText (返回 null)。");
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public Task<List<string>> GetParentBlockIdsAsync(string blockId)
    {
        Console.WriteLine($"[数据访问模拟] 请求获取 Block '{blockId}' 的父ID列表 (返回空列表)。");
        return Task.FromResult<List<string>>([]);
    }
}