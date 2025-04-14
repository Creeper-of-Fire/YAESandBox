using Microsoft.AspNetCore.Mvc;
using YAESandBox.API.DTOs;
using YAESandBox.API.Services;
using YAESandBox.Core.Action;
using YAESandBox.Core.State.Entity;
using YAESandBox.Depend; // For mapping DTO to Core object

namespace YAESandBox.API.Controllers;

[ApiController]
[Route("api/atomic/{blockId}")] // /api/atomic/{blockId}
public class AtomicController(BlockManager blockManager) : ControllerBase
{
    private BlockManager blockManager { get; } = blockManager;

    /// <summary>
    /// 对指定的 Block 执行一批原子化操作。
    /// 根据 Block 状态，操作可能被立即执行或暂存。
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]       // Operations executed successfully
    [ProducesResponseType(StatusCodes.Status202Accepted)]    // Operations queued due to block loading
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]    // e.g., Trying to modify while in conflict state without resolution
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // Invalid operations in request
    public async Task<IActionResult> ExecuteAtomicOperations(string blockId, [FromBody] BatchAtomicRequestDto request)
    {
        // 1. Map DTOs to Core AtomicOperation objects (add validation)
        var coreOperations = MapToCoreOperations(request.Operations);
        if (coreOperations == null) // Mapping/Validation failed
        {
            return BadRequest("Invalid atomic operations provided.");
        }

        // 2. Call BlockManager to handle the operations
        var result = await this.blockManager.EnqueueOrExecuteAtomicOperationsAsync(blockId, coreOperations);

        // 3. Return appropriate status code based on the result
        return result switch
        {
            AtomicExecutionResult.Executed => Ok("Operations executed successfully."), // Or 204 No Content if preferred
            AtomicExecutionResult.Queued => Accepted($"Operations queued for block '{blockId}' (block is loading)."),
            AtomicExecutionResult.NotFound => NotFound($"Block with ID '{blockId}' not found."),
            AtomicExecutionResult.ConflictState => Conflict($"Block '{blockId}' is in a conflict state. Resolve conflict first."),
            AtomicExecutionResult.Error => StatusCode(StatusCodes.Status500InternalServerError, "An error occurred during execution."),
            _ => StatusCode(StatusCodes.Status500InternalServerError, "An unexpected result occurred.")
        };
    }

    // Helper method to map DTOs (implement this properly with validation)
    private List<AtomicOperation>? MapToCoreOperations(List<AtomicOperationRequestDto> dtos)
    {
        var coreOps = new List<AtomicOperation>();
        foreach (var dto in dtos)
        {
            try
            {
                // Basic mapping example, add robust validation!
                var opType = Enum.Parse<AtomicOperationType>(dto.OperationType, true); // Case-insensitive parse
                AtomicOperation coreOp;
                switch (opType)
                {
                    case AtomicOperationType.CreateEntity:
                        coreOp = AtomicOperation.Create(dto.EntityType, dto.EntityId, dto.InitialAttributes);
                        break;
                    case AtomicOperationType.ModifyEntity:
                        if (string.IsNullOrWhiteSpace(dto.AttributeKey) || string.IsNullOrWhiteSpace(dto.ModifyOperator))
                            return null; // Invalid modify op
                        var op = OperatorSever.StringToOperator(dto.ModifyOperator);
                        coreOp = AtomicOperation.Modify(dto.EntityType, dto.EntityId, dto.AttributeKey, op, dto.ModifyValue);
                        break;
                    case AtomicOperationType.DeleteEntity:
                        coreOp = AtomicOperation.Delete(dto.EntityType, dto.EntityId);
                        break;
                    default: return null; // Unknown type
                }
                coreOps.Add(coreOp);
            }
            catch (Exception ex) // Catch parsing errors etc.
            {
                 Log.Error(ex,$"Failed to map AtomicOperationRequestDto: {ex.Message}");
                return null; // Indicate mapping failure
            }
        }
        return coreOps;
    }
}

// Helper enum for atomic execution results (can be defined elsewhere)
public enum AtomicExecutionResult { Executed, Queued, NotFound, ConflictState, Error }