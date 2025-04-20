using YAESandBox.API.DTOs;
using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.Depend;

namespace YAESandBox.API.Services.InterFaceAndBasic;

/// <summary>
/// 发送内容到前端，提醒前端需要更新某些东西了
/// </summary>
public interface INotifierService
{
    /// <summary>
    /// 发送Block状态更新到前端
    /// 收到这个讯号以后建议做一次全量更新
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="newStatusCode"></param>
    /// <returns></returns>
    Task NotifyBlockStatusUpdateAsync(string blockId, BlockStatusCode newStatusCode);

    /// <summary>
    /// 发送Block的状态更新到前端
    /// </summary>
    /// <param name="blockId">发生改变的block</param>
    /// <param name="changedFields">发生改变的字段</param>
    /// <param name="changedEntityIds">发生改变的实体</param>
    /// <returns></returns>
    Task NotifyBlockUpdateAsync(string blockId, IEnumerable<BlockDataFields> changedFields, IEnumerable<string> changedEntityIds);

    /// <inheritdoc cref="NotifyBlockUpdateAsync(string,IEnumerable{BlockDataFields},IEnumerable{string})"/>
    Task NotifyBlockUpdateAsync(string blockId, params string[] changedEntityIds)
    {
        return this.NotifyBlockUpdateAsync(blockId, [BlockDataFields.WorldState], changedEntityIds);
    }

    /// <inheritdoc cref="NotifyBlockUpdateAsync(string,IEnumerable{BlockDataFields},IEnumerable{string})"/>
    Task NotifyBlockUpdateAsync(string blockId, IEnumerable<string> changedEntityIds)
    {
        return this.NotifyBlockUpdateAsync(blockId, [BlockDataFields.WorldState], changedEntityIds);
    }

    /// <inheritdoc cref="NotifyBlockUpdateAsync(string,IEnumerable{BlockDataFields},IEnumerable{string})"/>
    Task NotifyBlockUpdateAsync(string blockId, params BlockDataFields[] changedFields)
    {
        return this.NotifyBlockUpdateAsync(blockId, changedFields, []);
    }

    /// <inheritdoc cref="NotifyBlockUpdateAsync(string,IEnumerable{BlockDataFields},IEnumerable{string})"/>
    [Obsolete("最好不要使用这个，而是写清楚所有的发生改变的字段", true)]
    Task NotifyBlockUpdateAsync(string blockId)
    {
        return this.NotifyBlockUpdateAsync(blockId, [], []);
    }
    
    /// <inheritdoc cref="NotifyBlockUpdateAsync(string,IEnumerable{BlockDataFields},IEnumerable{string})"/>
    Task NotifyBlockUpdateAllAsync(string blockId)
    {
        return this.NotifyBlockUpdateAsync(blockId, [], []);
    }

    /// <summary>
    /// 发送Block的细节更新到前端
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="changedFields">更新的字段。</param>
    /// <returns></returns>
    [Obsolete("目前我们不使用这个玩意，而是只通知可能发生变更的Block。", true)]
    Task NotifyBlockDetailUpdateAsync(string blockId, params BlockDetailFields[] changedFields);
    
    /// <summary>
    /// 检测到指令冲突就发送这个通知
    /// </summary>
    /// <param name="conflict"></param>
    /// <returns></returns>
    Task NotifyConflictDetectedAsync(ConflictDetectedDto conflict);
}