﻿using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.Services.InterFaceAndBasic;

namespace YAESandBox.API.Controllers;

/// <summary>
/// 带有 NotifierService 的基类
/// </summary>
/// <param name="readServices"></param>
/// <param name="writServices"></param>
[ApiController]
public class ApiControllerBase(
    IBlockReadService readServices,
    IBlockWritService writServices)
    : ControllerBase
{
    /// <summary>
    /// 写入服务
    /// </summary>
    protected IBlockReadService BlockReadService { get; } = readServices;

    /// <summary>
    /// 读取服务
    /// </summary>
    protected IBlockWritService BlockWritService { get; } = writServices;

    // /// <summary>
    // /// 通知服务
    // /// </summary>
    // protected INotifierService notifierService { get; } = notifierService;
}