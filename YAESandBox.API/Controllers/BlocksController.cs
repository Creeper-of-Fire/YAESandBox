using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.Core.Block;
using YAESandBox.Core.State;
using YAESandBox.Depend; // Assuming BlockManager is a service

namespace YAESandBox.API.Controllers;

/// <summary>
/// Block 相关的 API 控制器。
/// </summary>
/// <param name="writServices"></param>
/// <param name="readServices"></param>
[ApiController]
[Route("api/[controller]")] // /api/blocks
public class BlocksController(IBlockWritService writServices, IBlockReadService readServices) : ControllerBase
{
    private IBlockWritService WritServices { get; } = writServices;
    private IBlockReadService ReadServices { get; } = readServices;

    /// <summary>
    /// 获取所有 Block 的摘要信息字典。
    /// 返回一个以 Block ID 为键，Block 详细信息 DTO 为值的只读字典。
    /// </summary>
    /// <returns>包含所有 Block 详细信息的字典。</returns>
    /// <response code="200">成功返回 Block 字典。</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, BlockDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlocks()
    {
        var summaries = await this.ReadServices.GetAllBlockDetailsAsync();
        return this.Ok(summaries);
    }

    /// <summary>
    /// 获取指定 ID 的单个 Block 的详细信息（不包含 WorldState）。
    /// </summary>
    /// <param name="blockId">要查询的 Block 的唯一 ID。</param>
    /// <returns>指定 Block 的详细信息。</returns>
    /// <response code="200">成功返回 Block 详细信息。</response>
    /// <response code="404">未找到具有指定 ID 的 Block。</response>
    [HttpGet("{blockId}")]
    [ProducesResponseType(typeof(BlockDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlockDetail(string blockId)
    {
        var detail = await this.ReadServices.GetBlockDetailDtoAsync(blockId); // 实现这个方法
        if (detail == null)
        {
            return this.NotFound($"未找到 ID 为 '{blockId}' 的 Block。");
        }

        return this.Ok(detail);
    }

    /// <summary>
    /// 获取整个 Block 树的拓扑结构 (基于 ID 的嵌套关系)。
    /// 返回一个表示 Block 树层级结构的 JSON 对象。
    /// </summary>
    /// <returns>表示 Block 拓扑结构的 JSON 对象。</returns>
    /// <response code="200">成功返回 JSON 格式的拓扑结构。
    /// 形如：{ "id": "__WORLD__", "children": [{ "id": "child1", "children": [] },{ "id": "child2", "children": [] }] }</response>
    /// <response code="500">生成拓扑结构时发生内部服务器错误。</response>
    [HttpGet("topology")]
    [ProducesResponseType(typeof(BlockTopologyExporter.JsonBlockNode), StatusCodes.Status200OK, "application/json")]
    // 明确内容类型和返回类型
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
            Log.Error("GetTopology API: BlockReadService.GetBlockTopologyJsonAsync 返回值为空，生成失败。");
            return this.StatusCode(StatusCodes.Status500InternalServerError, "无法生成 Block 拓扑结构。");
        }
        catch (Exception ex)
        {
            // 捕获 Service 层或更深层可能抛出的未处理异常
            Log.Error(ex, "GetTopology API: 生成拓扑结构时发生意外错误。");
            return this.StatusCode(StatusCodes.Status500InternalServerError, "发生意外错误。");
        }
    }


    // 现在不会根据切换页面来发送信号了，页面状态仅在前端进行维护
    // 前端会将“当前选择路径的最底层block”通过“后端盲存”进行持久化，并且在启动时自行读取盲存数据解析路径
    // 在启动时，前端可能会调用后端的GetPathToRoot来生成路径，或者这个逻辑会被挪动到前端
}