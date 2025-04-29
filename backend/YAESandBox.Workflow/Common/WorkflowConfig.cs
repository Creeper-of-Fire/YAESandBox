// --- File: YAESandBox.Workflow/Common/WorkflowConfig.cs ---
using System.Collections.Generic;

namespace YAESandBox.Workflow.Common;

/// <summary>
/// 代表一个完整的工作流配置。
/// </summary>
/// <param name="Id">工作流的唯一标识符。</param>
/// <param name="Description">(可选) 工作流功能的描述。</param>
/// <param name="StepIds">按顺序执行的步骤 ID 列表。</param>
/// <param name="ExpectedTriggerParams">(可选, 文档性质) 描述期望的触发参数。</param>
public record WorkflowConfig(
    string Id,
    string? Description,
    List<string> StepIds, // 引用 StepConfig 的 ID
    Dictionary<string, string>? ExpectedTriggerParams = null // 用于文档和 GUI 提示
);