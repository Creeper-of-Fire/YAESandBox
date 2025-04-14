using System.Threading.Tasks;
using YAESandBox.Core.State;
using YAESandBox.API.DTOs;

namespace YAESandBox.API.Services;

public interface INotifierService
{
    Task NotifyBlockStatusUpdateAsync(string blockId, BlockStatus newStatus);
    Task NotifyStateUpdateAsync(string blockId, IEnumerable<string>? changedEntityIds = null);
    Task NotifyWorkflowUpdateAsync(WorkflowUpdateDto update); // <--- 添加
    Task NotifyWorkflowCompleteAsync(WorkflowCompleteDto completion); // <--- 添加
    Task NotifyConflictDetectedAsync(ConflictDetectedDto conflict); // <--- 添加
}