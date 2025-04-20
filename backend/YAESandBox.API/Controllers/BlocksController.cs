using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.API.Services.InterFaceAndBasic;
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
public class BlocksController(
    IBlockWritService writServices,
    IBlockReadService readServices,
    INotifierService notifierService)
    : APINotifyControllerBase(readServices, writServices, notifierService)
{
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
        var summaries = await this.blockReadService.GetAllBlockDetailsAsync();
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
        var detail = await this.blockReadService.GetBlockDetailDtoAsync(blockId); // 实现这个方法
        if (detail == null) return this.NotFound($"未找到 ID 为 '{blockId}' 的 Block。");

        return this.Ok(detail);
    }

    /// <summary>
    /// 获取整个 Block 树的拓扑结构 (基于 ID 的嵌套关系)。
    /// 返回一个表示 Block 树层级结构的 JSON 对象。
    /// </summary>
    /// <param name="blockId">目标根节点的ID，如果为空则返回整个父节点的ID</param>
    /// <returns>表示 Block 拓扑结构的 JSON 对象。</returns>
    /// <response code="200">成功返回 JSON 格式的拓扑结构。
    /// 形如：{ "id": "__WORLD__", "children": [{ "id": "child1", "children": [] },{ "id": "child2", "children": [] }] }</response>
    /// <response code="500">生成拓扑结构时发生内部服务器错误。</response>
    [HttpGet("topology")]
    [ProducesResponseType(typeof(BlockTopologyExporter.JsonBlockNode), StatusCodes.Status200OK, "application/json")]
    // 明确内容类型和返回类型
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTopology(string? blockId)
    {
        try
        {
            // 调用 Service 获取 JSON 字符串
            var topologyJson = await this.blockReadService.GetBlockTopologyJsonAsync(blockId);

            if (topologyJson != null)
                // 直接返回 JSON 字符串，设置正确的 ContentType
                // ContentResult 会自动处理字符串内容和 ContentType
                return this.Ok(topologyJson);

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

    /// <summary>
    /// 部分更新指定 Block 的内容和/或元数据。
    /// 此操作仅在 Block 处于 Idle 状态时被允许。
    /// </summary>
    /// <param name="blockId">要更新的 Block 的 ID。</param>
    /// <param name="updateDto">包含要更新的字段（Content, MetadataUpdates）的请求体。
    /// 省略的字段或值为 null 的字段将不会被修改（MetadataUpdates 中值为 null 表示移除该键）。</param>
    /// <returns>无内容响应表示成功。</returns>
    /// <response code="204">更新成功。</response>
    /// <response code="400">请求体无效或未提供任何更新。</response>
    /// <response code="404">未找到具有指定 ID 的 Block。</response>
    /// <response code="409">Block 不处于 Idle 状态，无法修改。</response>
    /// <response code="500">更新时发生内部服务器错误。</response>
    [HttpPatch("{blockId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)] // 使用 409 表示状态冲突
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateBlockDetails(string blockId, [FromBody] UpdateBlockDetailsDto updateDto)
    {
        if (!this.ModelState.IsValid) // 基本模型验证
            return this.BadRequest(this.ModelState);

        // 可以在这里添加一个检查，确保至少提供了一项更新
        if (updateDto.Content == null && (updateDto.MetadataUpdates == null || !updateDto.MetadataUpdates.Any()))
            return BadRequest("必须提供 Content 或 MetadataUpdates 中的至少一项来进行更新。");

        var result = await this.blockWritService.UpdateBlockDetailsAsync(blockId, updateDto);

        if (updateDto.Content != null)
            await this.notifierService.NotifyBlockUpdateAsync(blockId, BlockDataFields.BlockContent);
        if (updateDto.MetadataUpdates != null && updateDto.MetadataUpdates.Any())
            await this.notifierService.NotifyBlockUpdateAsync(blockId, BlockDataFields.Metadata);

        return result switch
        {
            BlockResultCode.Success => this.NoContent(), // 204
            BlockResultCode.NotFound => this.NotFound($"未找到 ID 为 '{blockId}' 的 Block。"), // 404
            BlockResultCode.InvalidState => this.Conflict($"Block '{blockId}' 当前状态不允许修改内容或元数据。请确保其处于 Idle 状态。"), // 409
            BlockResultCode.InvalidInput => this.BadRequest("无效的更新操作。"), // 400 (如果服务层返回这个)
            _ => this.StatusCode(StatusCodes.Status500InternalServerError, "更新 Block 时发生意外错误。") // 500
        };
    }
}