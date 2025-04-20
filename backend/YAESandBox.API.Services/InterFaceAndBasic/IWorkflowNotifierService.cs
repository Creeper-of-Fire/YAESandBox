using YAESandBox.API.DTOs;
using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.Depend;

namespace YAESandBox.API.Services.InterFaceAndBasic;

/// <summary>
/// 发送内容到前端，提醒前端需要更新某些东西了
/// </summary>
public interface IWorkflowNotifierService
{
    /// <summary>
    /// 发送显示更新到前端
    /// </summary>
    /// <param name="update"></param>
    /// <returns></returns>
    Task NotifyDisplayUpdateAsync(DisplayUpdateDto update);
}