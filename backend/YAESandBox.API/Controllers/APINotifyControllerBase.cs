using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.Services.InterFaceAndBasic;

namespace YAESandBox.API.Controllers;

/// <summary>
/// 带有 NotifierService 的基类
/// </summary>
/// <param name="readServices"></param>
/// <param name="writServices"></param>
/// <param name="notifierService"></param>
[ApiController]
public class APINotifyControllerBase(IBlockReadService readServices, IBlockWritService writServices, INotifierService notifierService)
    : ControllerBase
{
    /// <summary>
    /// 写入服务
    /// </summary>
    protected IBlockReadService blockReadService { get; } = readServices;

    /// <summary>
    /// 读取服务
    /// </summary>
    protected IBlockWritService blockWritService { get; } = writServices;

    /// <summary>
    /// 通知服务
    /// </summary>
    protected INotifierService notifierService { get; } = notifierService;
}