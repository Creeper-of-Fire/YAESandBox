// --- File: YAESandBox.Workflow/Implementation/PromptManager.cs ---
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Microsoft.Extensions.Logging;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.Common;

namespace YAESandBox.Workflow.Implementation;

/// <summary>
/// IPromptManager 的具体实现。
/// </summary>
internal class PromptManager : IPromptManager
{
    private readonly ITemplateRenderer _templateRenderer;
    private readonly Func<dynamic> _getGlobals; // 获取 Globals 的委托
    private readonly Func<dynamic> _getLocals;  // 获取 Locals 的委托
    private readonly Action<string, LogLevel> _logCallback;

    private List<PromptMessage> _messages = new();

    // 使用委托来访问动态变化的 Globals 和 Locals
    public PromptManager(
        ITemplateRenderer templateRenderer,
        Func<dynamic> getGlobals,
        Func<dynamic> getLocals,
        Action<string, LogLevel> logCallback)
    {
        this._templateRenderer = templateRenderer;
        this._getGlobals = getGlobals;
        this._getLocals = getLocals;
        this._logCallback = logCallback;
    }

     private dynamic Globals => _getGlobals();
     private dynamic Locals => _getLocals();

    public void Add(string templateContent, string role)
    {
        _messages.Add(new PromptMessage(Role: role, TemplateContent: templateContent));
        Log($"添加模板提示: Role={role}, Template Length={templateContent?.Length ?? 0}", LogLevel.Debug);
    }

    public void AddRaw(string rawContent, string role)
    {
        // 对于原始文本，也存储在 TemplateContent 中，渲染时直接返回即可
        _messages.Add(new PromptMessage(Role: role, TemplateContent: rawContent, RenderedContent: rawContent));
         Log($"添加原始提示: Role={role}, Content Length={rawContent?.Length ?? 0}", LogLevel.Debug);
    }

    public void Clear()
    {
        _messages.Clear();
        Log("提示列表已清空", LogLevel.Debug);
    }

    public IReadOnlyList<PromptMessage> GetRenderedMessages()
    {
        // 渲染所有尚未渲染的消息
        var renderedMessages = new List<PromptMessage>(_messages.Count);
        var variables = MergeVariables(); // 获取当前变量快照

        foreach (var msg in _messages)
        {
            if (msg.RenderedContent == null) // 仅渲染未渲染的
            {
                string rendered = _templateRenderer.Render(msg.TemplateContent, variables);
                renderedMessages.Add(msg with { RenderedContent = rendered }); // 创建新的 record 实例
            }
            else
            {
                renderedMessages.Add(msg); // 已经是原始文本或已渲染
            }
        }
        // 注意：这里返回的是渲染后的快照，但 _messages 内部存储的 RenderedContent 也被更新了
        // 如果需要原始的、未渲染的列表，需要提供另一个方法
        // 或者在 GetRenderedMessages 内部不修改 _messages，而是返回一个全新的列表
        // 当前实现会更新内部状态，下次调用 GetRenderedMessages 如果变量没变，不会重新渲染
        _messages = renderedMessages; // 将渲染结果存回内部列表 (简化逻辑)
        return _messages.AsReadOnly();
    }

    public void SaveToGlobals(string variableName)
    {
        // 保存原始模板列表到 Globals
        // 注意：需要确保 ExpandoObject 能正确处理 List<PromptMessage>
        // 序列化可能需要特殊处理 record 类型
        try
        {
             // 直接赋值给 dynamic 对象
             ((IDictionary<string, object?>)Globals)[variableName] = new List<PromptMessage>(_messages); // 保存副本
             Log($"提示列表已保存到全局变量 '{variableName}'", LogLevel.Debug);
        }
        catch (Exception ex)
        {
             Log($"保存提示到全局变量 '{variableName}' 时出错: {ex.Message}", LogLevel.Error);
        }
    }

    public void LoadFromGlobals(string variableName)
    {
       try
       {
            var loadedList = ((IDictionary<string, object?>)Globals)[variableName] as List<PromptMessage>;
            if (loadedList != null)
            {
                // 加载时，清除 RenderedContent，以便使用当前变量重新渲染
                _messages = loadedList.Select(m => m with { RenderedContent = null }).ToList();
                Log($"已从全局变量 '{variableName}' 加载提示列表", LogLevel.Debug);
            }
            else
            {
                 Log($"从全局变量 '{variableName}' 加载提示列表失败，未找到或类型不匹配", LogLevel.Warning);
                 _messages.Clear(); // 加载失败则清空
            }
       }
       catch (Exception ex)
       {
            Log($"从全局变量 '{variableName}' 加载提示时出错: {ex.Message}", LogLevel.Error);
            _messages.Clear(); // 出错则清空
       }
    }

     // 辅助方法：合并 Globals 和 Locals
    private IReadOnlyDictionary<string, object?> MergeVariables()
    {
        var variables = new Dictionary<string, object?>();
        var globalDict = Globals as IDictionary<string, object?>;
        var localDict = Locals as IDictionary<string, object?>;

        if (globalDict != null) foreach (var kvp in globalDict) variables[kvp.Key] = kvp.Value;
        if (localDict != null) foreach (var kvp in localDict) variables[kvp.Key] = kvp.Value; // Locals 优先

        return variables;
    }

     private void Log(string message, LogLevel level)
     {
         _logCallback($"[PromptManager] {message}", level);
     }
}
