using YAESandBox.API.DTOs;
using YAESandBox.Depend;

namespace YAESandBox.API.Services;


/// <summary>
/// 发送内容到前端，提醒前端需要更新某些东西了
/// </summary>
public interface INotifierService
{
    /// <summary>
    /// 发送Block状态更新到前端
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="newStatusCode"></param>
    /// <returns></returns>
    Task NotifyBlockStatusUpdateAsync(string blockId, BlockStatusCode newStatusCode);

    /// <summary>
    /// 发送State状态更新到前端
    /// </summary>
    /// <param name="blockId"></param>
    /// <param name="changedEntityIds"></param>
    /// <returns></returns>
    Task NotifyStateUpdateAsync(string blockId, IEnumerable<string>? changedEntityIds = null);

    /// <summary>
    /// 发送显示更新到前端
    /// </summary>
    /// <param name="update"></param>
    /// <returns></returns>
    Task NotifyDisplayUpdateAsync(DisplayUpdateDto update);

    /// <summary>
    /// 检测到指令冲突就发送这个通知
    /// </summary>
    /// <param name="conflict"></param>
    /// <returns></returns>
    Task NotifyConflictDetectedAsync(ConflictDetectedDto conflict);
}