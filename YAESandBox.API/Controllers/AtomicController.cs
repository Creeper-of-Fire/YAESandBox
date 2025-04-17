using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.Core.Action;
using YAESandBox.Core.State;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend; // For mapping DTO to Core object

namespace YAESandBox.API.Controllers;

/// <summary>
/// 处理原子操作的 API 控制器。
/// </summary>
[ApiController]
[Route("api/atomic/{blockId}")] // /api/atomic/{blockId}
public class AtomicController(IBlockWritService writServices, IBlockReadService readServices) : ControllerBase
{
    private IBlockWritService blockWritService { get; } = writServices;
    private IBlockReadService blockReadService { get; } = readServices;

    /// <summary>
    /// 对指定的 Block 执行一批原子化操作。
    /// 根据 Block 的当前状态，操作可能被立即执行或暂存。
    /// </summary>
    /// <param name="blockId">要执行操作的目标 Block 的 ID。</param>
    /// <param name="request">包含原子操作列表的请求体。</param>
    /// <returns>指示操作执行结果的 HTTP 状态码。</returns>
    /// <response code="200">操作已成功执行 (适用于 Idle 状态)。</response>
    /// <response code="202">操作已成功执行并/或已暂存 (适用于 Loading 状态)。</response>
    /// <response code="400">请求中包含无效的原子操作定义。</response>
    /// <response code="404">未找到具有指定 ID 的 Block。</response>
    /// <response code="409">Block 当前处于冲突状态 (ResolvingConflict)，需要先解决冲突。</response>
    /// <response code="500">执行操作时发生内部服务器错误。</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExecuteAtomicOperations(string blockId, [FromBody] BatchAtomicRequestDto request)
    {
        // 1. 使用辅助方法将 DTO 映射为核心原子操作对象
        List<AtomicOperation> coreOperations;
        try
        {
            // 调用 DTO 辅助类中的扩展方法
            coreOperations = request.Operations.ToAtomicOperations();
        }
        // 捕获由 ToAtomicOperations (及其调用的 ToAtomicOperation 和 AtomicOperation 工厂方法)
        // 抛出的验证或解析异常 (例如无效的操作类型、操作符、空ID等)
        catch (ArgumentException ex)
        {
            Log.Warning($"原子操作映射失败: {ex.Message}");
            // 返回 400 Bad Request，指示请求数据有问题
            return this.BadRequest($"提供的原子操作无效: {ex.Message}");
        }
        // 捕获其他意外异常
        catch (Exception ex)
        {
            Log.Error(ex, $"映射原子操作时发生意外错误: {ex.Message}");
            return this.StatusCode(StatusCodes.Status500InternalServerError, "处理请求时发生内部错误。");
        }

        // 2. 调用 BlockManager 处理操作
        var result = await this.blockWritService.EnqueueOrExecuteAtomicOperationsAsync(blockId, coreOperations);

        // 3. 根据结果返回相应的状态码
        return result switch
        {
            AtomicExecutionResult.Executed => this.Ok("操作已成功执行。"), // 200 OK
            AtomicExecutionResult.ExecutedAndQueued => this.Accepted(null as string, // 使用 Accepted(string?, string?) 重载
                $"操作已成功执行。部分或全部操作已为 Block '{blockId}' (Loading 状态) 排队等待。"),
            // 202 Accepted
            AtomicExecutionResult.NotFound => this.NotFound($"未找到 ID 为 '{blockId}' 的 Block。"),
            // 404 Not Found
            AtomicExecutionResult.ConflictState => this.Conflict($"Block '{blockId}' 处于冲突状态。请先解决冲突。"),
            // 409 Conflict
            AtomicExecutionResult.Error => this.StatusCode(StatusCodes.Status500InternalServerError, "执行期间发生错误。"),
            // 500 Internal Server Error
            _ => this.StatusCode(StatusCodes.Status500InternalServerError, "发生意外的结果。")
            // 500 Internal Server Error for unknown enum value
        };
    }

    // /// <summary>
    // /// 辅助方法：将原子操作请求 DTO 列表映射为核心原子操作对象列表。
    // /// </summary>
    // /// <param name="dtos">原子操作请求 DTO 列表。</param>
    // /// <returns>核心原子操作对象列表，如果任何 DTO 无效则返回 null。</returns>
    // private List<AtomicOperation>? MapToCoreOperations(List<AtomicOperationRequestDto> dtos)
    // {
    //     var coreOps = new List<AtomicOperation>();
    //     foreach (var dto in dtos)
    //     {
    //         try
    //         {
    //             // 基础映射示例，需要添加更健壮的验证！
    //             if (!Enum.TryParse<AtomicOperationType>(dto.OperationType, true, out var opType))
    //             {
    //                 Log.Warning($"无法解析原子操作类型: {dto.OperationType}");
    //                 return null; // 无效的操作类型
    //             }
    //
    //             AtomicOperation coreOp;
    //             switch (opType)
    //             {
    //                 case AtomicOperationType.CreateEntity:
    //                     if (string.IsNullOrWhiteSpace(dto.EntityId)) return null; // ID 不能为空
    //                     coreOp = AtomicOperation.Create(dto.EntityType, dto.EntityId, dto.InitialAttributes);
    //                     break;
    //                 case AtomicOperationType.ModifyEntity:
    //                     if (string.IsNullOrWhiteSpace(dto.EntityId) ||
    //                         string.IsNullOrWhiteSpace(dto.AttributeKey) ||
    //                         string.IsNullOrWhiteSpace(dto.ModifyOperator))
    //                     {
    //                         Log.Warning(
    //                             $"Modify 操作缺少必要的参数: EntityId={dto.EntityId}, AttributeKey={dto.AttributeKey}, Operator={dto.ModifyOperator}");
    //                         return null; // 无效的 modify 操作
    //                     }
    //
    //                     // 注意：这里的 ModifyValue 可以是 null，取决于业务逻辑是否允许设置 null
    //                     Operator op;
    //                     try
    //                     {
    //                         op = OperatorHelper.StringToOperator(dto.ModifyOperator);
    //                     }
    //                     catch (ArgumentException ex)
    //                     {
    //                         Log.Warning($"无效的修改操作符 '{dto.ModifyOperator}': {ex.Message}");
    //                         return null;
    //                     }
    //
    //                     coreOp = AtomicOperation.Modify(dto.EntityType, dto.EntityId, dto.AttributeKey, op,
    //                         dto.ModifyValue);
    //                     break;
    //                 case AtomicOperationType.DeleteEntity:
    //                     if (string.IsNullOrWhiteSpace(dto.EntityId)) return null; // ID 不能为空
    //                     coreOp = AtomicOperation.Delete(dto.EntityType, dto.EntityId);
    //                     break;
    //                 default:
    //                     Log.Warning($"未知的原子操作类型枚举值: {opType}");
    //                     return null; // 未知的类型
    //             }
    //
    //             coreOps.Add(coreOp);
    //         }
    //         catch (Exception ex) // 捕获解析错误等。
    //         {
    //             Log.Error(ex, $"映射 AtomicOperationRequestDto 时失败: {ex.Message}");
    //             return null; // 指示映射失败
    //         }
    //     }
    //
    //     return coreOps;
    // }
}

/// <summary>
/// 表示原子操作执行结果的枚举。
/// </summary>
public enum AtomicExecutionResult
{
    /// <summary>
    /// 操作已成功执行 (通常在 Idle 状态下)。
    /// </summary>
    Executed,

    /// <summary>
    /// 操作已成功执行并且/或者已暂存 (通常在 Loading 状态下)。
    /// </summary>
    ExecutedAndQueued,

    /// <summary>
    /// 目标 Block 未找到。
    /// </summary>
    NotFound,

    /// <summary>
    /// Block 当前处于冲突状态，无法执行操作。
    /// </summary>
    ConflictState,

    /// <summary>
    /// 执行过程中发生错误。
    /// </summary>
    Error
}