using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.Core.State;
using YAESandBox.Depend; // Assuming BlockManager is a service

namespace YAESandBox.API.Controllers;

[ApiController]
[Route("api/[controller]")] // /api/blocks
public class BlocksController(IBlockWritService writServices, IBlockReadService readServices) : ControllerBase
{
    private IBlockWritService WritServices { get; } = writServices;
    private IBlockReadService ReadServices { get; } = readServices;

    /// <summary>
    /// 获取 Block 树的{id,详细信息}键值对字典。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, BlockDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlocks()
    {
        var summaries = await this.ReadServices.GetAllBlockDetailsAsync();
        return this.Ok(summaries);
    }

    /// <summary>
    /// 获取单个 Block 的详细信息（不含 WorldState）。
    /// </summary>
    [HttpGet("{blockId}")]
    [ProducesResponseType(typeof(BlockDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlockDetail(string blockId)
    {
        var detail = await this.ReadServices.GetBlockDetailDtoAsync(blockId); // 实现这个方法
        if (detail == null)
        {
            return this.NotFound($"Block with ID '{blockId}' not found.");
        }

        return this.Ok(detail);
    }
    
    /// <summary>
    /// 获取整个 Block 树的拓扑结构 (基于 ID 的嵌套关系)。
    /// </summary>
    [HttpGet("topology")] // <<< 新增的路由
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK, "application/json")] // 明确内容类型
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTopology()
    {
        try
        {
            // 调用 Service 获取 JSON 字符串
            var topologyJson = await this.ReadServices.GetBlockTopologyJsonAsync();

            if (topologyJson != null)
            {
                // 直接返回 JSON 字符串，设置正确的 ContentType
                // ContentResult 会自动处理字符串内容和 ContentType
                return this.Ok(topologyJson);
            }

            // 如果 service 返回 null，表示生成失败
            Log.Error("GetTopology API: BlockReadService.GetBlockTopologyJsonAsync returned null.");
            return this.StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate block topology.");
        }
        catch (Exception ex)
        {
            // 捕获 Service 层或更深层可能抛出的未处理异常
            Log.Error(ex, "GetTopology API: An unexpected error occurred while generating topology.");
            return this.StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }


    // 现在不会根据切换页面来发送信号了，页面状态仅在前端进行维护
    // 前端会将“当前选择路径的最底层block”通过“后端盲存”进行持久化，并且在启动时自行读取盲存数据解析路径
    // 在启动时，前端可能会调用后端的GetPathToRoot来生成路径，或者这个逻辑会被挪动到前端
}