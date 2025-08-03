using YAESandBox.Seed.DTOs;
using YAESandBox.Seed.DTOs.WebSocket;

namespace YAESandBox.Seed.API.Hubs;

public interface IGameClient
{
    /// <summary>
    /// Block的状态码更新了
    /// 收到这个讯号以后建议做一次全量更新
    /// </summary>
    /// <param name="update"></param>
    /// <returns></returns>
    Task ReceiveBlockStatusUpdate(BlockStatusUpdateDto update);

    /// <summary>
    /// 更新特定Block/控件的显示内容
    /// </summary>
    /// <param name="update"></param>
    /// <returns></returns>
    Task ReceiveDisplayUpdate(DisplayUpdateDto update);

    /// <summary>
    /// 检测到主工作流存在冲突
    /// </summary>
    /// <param name="blockId"></param>
    /// <returns></returns>
    Task ReceiveConflictDetected(string blockId);

    /// <summary>
    /// Block内部数据存在更新，建议重新获取
    /// </summary>
    /// <param name="signal"></param>
    /// <returns></returns>
    Task ReceiveBlockUpdateSignal(BlockUpdateSignalDto signal);

    /// <summary>
    /// Block的非WorldState/GameState内容存在更新，建议重新获取
    /// </summary>
    /// <param name="signal"></param>
    /// <returns></returns>
    Task ReceiveBlockUpdateSignal(string signal);

    /// <summary>
    /// Block的详细信息更新了，比如显示内容、父子结构或者Metadata（不包含DisplayUpdate发起的那些内容更新）
    /// </summary>
    /// <param name="partiallyFilledDto">部分填充的 BlockDetailDto，包含各种已更新的字段。</param>
    /// <returns></returns>
    [Obsolete("目前我们不使用这个玩意，而是只通知可能发生变更的Block。", true)]
    Task ReceiveBlockDetailUpdateSignal(BlockDetailDto partiallyFilledDto);
}