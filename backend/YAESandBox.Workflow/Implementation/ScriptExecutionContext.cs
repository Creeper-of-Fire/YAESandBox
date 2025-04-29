// --- File: YAESandBox.Workflow/Implementation/ScriptExecutionContext.cs ---
using System;
using System.Collections.Generic;
using System.Dynamic;
using Microsoft.Extensions.Logging; // For ExpandoObject
using YAESandBox.Core.Action;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.Common;

namespace YAESandBox.Workflow.Implementation;

/// <summary>
/// IScriptExecutionContext 的具体实现。
/// </summary>
internal class ScriptExecutionContext : IScriptExecutionContext
{
    private readonly IWorkflowDataAccess _dataAccess;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly Action<DisplayUpdateRequestPayload> _requestDisplayUpdateCallback;
    private readonly Action<string, LogLevel> _logCallback;
    private readonly List<AtomicOperation> _accumulatedOperations; // 引用引擎的列表

    // --- 构造函数 ---
    public ScriptExecutionContext(
        dynamic globals,
        dynamic locals,
        IPromptManager promptManager,
        IReadOnlyDictionary<string, string> triggerParams,
        IWorkflowDataAccess dataAccess,
        ITemplateRenderer templateRenderer,
        Action<DisplayUpdateRequestPayload> requestDisplayUpdateCallback,
        Action<string, LogLevel> logCallback,
        List<AtomicOperation> accumulatedOperations, // 传递引擎的列表引用
        string? aiFinalOutput = null, // 步骤执行时传入
        string? aiStreamChunk = null, // 流处理回调时传入
        string? aiStreamTotal = null, // 流处理回调时传入
        long stepExecutionTimeMs = 0 // 步骤执行时传入
        )
    {
        this.Globals = globals;
        this.Locals = locals;
        this.Prompts = promptManager;
        this.TriggerParams = triggerParams;
        this._dataAccess = dataAccess;
        this._templateRenderer = templateRenderer;
        this._requestDisplayUpdateCallback = requestDisplayUpdateCallback;
        this._logCallback = logCallback;
        this._accumulatedOperations = accumulatedOperations;
        this.AiFinalOutput = aiFinalOutput;
        this.AiStreamChunk = aiStreamChunk;
        this.AiStreamTotal = aiStreamTotal;
        this.StepExecutionTimeMs = stepExecutionTimeMs;
    }

    // --- IScriptExecutionContext 实现 ---
    public dynamic Globals { get; }
    public dynamic Locals { get; }
    public IPromptManager Prompts { get; }
    public IReadOnlyDictionary<string, string> TriggerParams { get; }
    public string? AiFinalOutput { get; internal set; } // 允许引擎在 AI 调用后设置
    public string? AiStreamChunk { get; internal set; } // 允许引擎在流回调时设置
    public string? AiStreamTotal { get; internal set; } // 允许引擎在流回调时设置
    public long StepExecutionTimeMs { get; internal set; } // 允许引擎更新

    public IWorkflowDataAccess DataAccess => this._dataAccess;

    public void AddAtomicOperation(AtomicOperation operation)
    {
        // 添加到引擎维护的总列表
        this._accumulatedOperations.Add(operation);
        this.Log($"添加原子操作: {operation.OperationType} on {operation.EntityType}:{operation.EntityId}", LogLevel.Debug);
    }

    public void RequestDisplayUpdate(DisplayUpdateRequestPayload payload)
    {
        this._requestDisplayUpdateCallback(payload);
        this.Log($"请求显示更新: Mode={payload.UpdateMode}, Target={payload.TargetElementId ?? "Main"}, Content Length={payload.Content?.Length ?? 0}", LogLevel.Debug);
    }

    public string RenderTemplate(string template)
    {
        // 合并 Globals 和 Locals 变量到一个字典供渲染器使用
        // 注意： ExpandoObject 到 Dictionary 的转换，以及同名变量的优先级（例如 Locals 覆盖 Globals）
        var variables = new Dictionary<string, object?>();

        // 尝试从 ExpandoObject 获取字典视图
        var globalDict = this.Globals as IDictionary<string, object?>;
        var localDict = this.Locals as IDictionary<string, object?>;

        if (globalDict != null)
        {
            foreach (var kvp in globalDict)
            {
                variables[kvp.Key] = kvp.Value;
            }
        }
        if (localDict != null)
        {
            foreach (var kvp in localDict)
            {
                variables[kvp.Key] = kvp.Value; // Locals 覆盖 Globals
            }
        }

        return this._templateRenderer.Render(template, variables);
    }

    public void Log(string message, LogLevel level = LogLevel.Information)
    {
        // 调用注入的回调，可以添加上下文信息
        this._logCallback($"[ScriptContext] {message}", level);
    }
}