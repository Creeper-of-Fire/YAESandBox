using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAESandBox.Core.DTOs;
using YAESandBox.Core.Services.InterFaceAndBasic;
using YAESandBox.Depend;

// Assuming BlockManager is a service

namespace YAESandBox.Core.API.Controllers;

/// <summary>
/// Block 相关的 API 控制器。
/// </summary>
/// <param name="writServices"></param>
/// <param name="readServices"></param>
[ApiController]
[Route("api/[controller]")] // /api/blocks
[ApiExplorerSettings(GroupName = GlobalSwaggerConstants.PublicApiGroupName)]
public class BlocksController(IBlockWritService writServices, IBlockReadService readServices)
    : ApiControllerBase(readServices, writServices)
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
        var summaries = await this.BlockReadService.GetAllBlockDetailsAsync();
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
        var detail = await this.BlockReadService.GetBlockDetailDtoAsync(blockId); // 实现这个方法
        if (detail == null) return this.NotFound($"未找到 ID 为 '{blockId}' 的 Block。");

        return this.Ok(detail);
    }

    /// <summary>
    /// 获取扁平化的 Block 拓扑结构信息。
    /// 返回一个包含所有 Block (或指定子树下所有 Block) 的拓扑信息的列表，
    /// 每个对象包含其 ID 和父节点 ID，用于在客户端重建层级关系。
    /// </summary>
    /// <param name="blockId">
    /// （可选）目标根节点的 ID。
    /// 如果提供，则返回以此节点为根的子树（包含自身）的扁平拓扑信息。
    /// 如果为 null 或空，则返回从最高根节点 (__WORLD__) 开始的整个应用的完整扁平拓扑结构。
    /// </param>
    /// <returns>包含 Block 节点信息的扁平列表。</returns>
    /// <response code="200">成功返回扁平化的拓扑节点列表。
    /// 列表中的每个对象形如：{ "blockId": "some-id", "parentBlockId": "parent-id" } 或 { "blockId": "__WORLD__", "parentBlockId": null }。
    /// 例如：[ { "blockId": "__WORLD__", "parentBlockId": null }, { "blockId": "child1", "parentBlockId": "__WORLD__" }, ... ]
    /// </response>
    /// <response code="404">如果指定了 blockId，但未找到具有该 ID 的 Block。</response>
    /// <response code="500">获取拓扑结构时发生内部服务器错误。</response>
    [HttpGet("topology")]
    [ProducesResponseType(typeof(List<BlockTopologyNodeDto>), StatusCodes.Status200OK)] // 返回 DTO 列表
    [ProducesResponseType(StatusCodes.Status404NotFound)] // 添加 404
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<BlockTopologyNodeDto>>> GetTopology(string? blockId) // 方法名修改，返回类型修改
    {
        try
        {
            // 调用 Service 获取扁平列表 (需要 BlockReadService 实现新方法)
            var flatTopologyList = await this.BlockReadService.GetBlockTopologyListAsync(blockId);

            // 检查 Service 返回结果
            if (flatTopologyList.Any())
                return this.Ok(flatTopologyList); // 成功获取到列表 (即使是空列表也算成功)

            // 如果提供了 blockId 但 Service 返回 null，通常意味着 blockId 不存在
            if (!string.IsNullOrEmpty(blockId))
            {
                Log.Warning($"GetFlatTopology API: 未找到指定的根节点 blockId '{blockId}'。");
                return this.NotFound($"未找到指定的根节点 blockId '{blockId}'。");
            }

            // 如果 blockId 为空但仍然返回 null，说明获取整个拓扑失败
            Log.Error("GetFlatTopology API: BlockReadService.GetBlockHierarchyAsync 返回 null，获取完整拓扑失败。");
            return this.StatusCode(StatusCodes.Status500InternalServerError, "无法获取 Block 拓扑结构。");
        }
        catch (ArgumentException ex) // 可以捕获更具体的服务层异常，例如 ID 不存在
        {
            Log.Warning(ex, $"GetFlatTopology API: 处理请求时参数无效 (可能 blockId 不存在)。 BlockId: {blockId}");
            return this.NotFound($"未找到或处理节点 blockId '{blockId}' 时出错。");
        }
        catch (Exception ex)
        {
            // 捕获其他未处理异常
            Log.Error(ex, $"GetFlatTopology API: 获取扁平拓扑结构时发生意外错误。BlockId: {blockId}");
            return this.StatusCode(StatusCodes.Status500InternalServerError, "获取拓扑结构时发生意外错误。");
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

        var result = await this.BlockWritService.UpdateBlockDetailsAsync(blockId, updateDto);

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