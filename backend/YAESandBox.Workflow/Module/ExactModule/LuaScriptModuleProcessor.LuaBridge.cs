using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Step.StepProcessor;
// ReSharper disable InconsistentNaming

namespace YAESandBox.Workflow.Module.ExactModule;

internal partial class LuaScriptModuleProcessor
{
    /// <summary>
    /// 作为 C# 与 Lua 脚本之间交互的安全桥梁。
    /// 仅暴露 Get 和 Set 方法，防止 Lua 脚本访问不应访问的内部状态。
    /// </summary>
    private class LuaContextBridge(StepProcessorContent stepContent,LuaLogBridge logger)
    {
        private StepProcessorContent StepContent { get; } = stepContent;
        private LuaLogBridge Logger { get; } = logger;

        /// <summary>
        /// 从步骤变量池中获取一个变量。暴露给 Lua 使用。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <returns>变量的值，如果不存在则为 null。</returns>
        public object? get(string name)
        {
            try
            {
                var rawValue = this.StepContent.InputVar(name);
                if (rawValue == null) return null;

                // --- 安全净化 ---
                // 无论原始对象是什么类型，都将其序列化为 JSON，然后再反序列化为 NLua 兼容的纯数据结构
                // 这是防止不兼容的 C# 对象泄漏到 Lua 的关键。
                var jsonString = JsonSerializer.Serialize(rawValue);
                return LuaJsonBridge.ConvertJsonToLuaCompatible(jsonString, Logger);
            }
            catch (Exception ex)
            {
                Logger.error($"ctx.get('{name}') 失败: {ex.Message}");
                return null; // 向 Lua 返回 nil
            }
        }

        /// <summary>
        ///向步骤变量池中设置一个变量。暴露给 Lua 使用。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <param name="value">要设置的值。</param>
        public void set(string name, object value)
        {
            try
            {
                this.StepContent.OutputVar(name, value);
            }
            catch (Exception ex)
            {
                Logger.error($"ctx.set('{name}') 失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 向 Lua 暴露基于 System.Text.Json 的功能。
    /// </summary>
    private class LuaJsonBridge(LuaLogBridge logger)
    {
        private LuaLogBridge Logger { get; } = logger;

        public string? encode(object? value)
        {
            try
            {
                return value == null ? null : JsonSerializer.Serialize(value);
            }
            catch (Exception ex)
            {
                Logger.error($"json.encode 失败: {ex.Message}");
                return null;
            }
        }

        public object? decode(string? json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return null;
                return ConvertJsonToLuaCompatible(json, Logger);
            }
            catch (Exception ex)
            {
                Logger.error($"json.decode 失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 核心辅助方法：将 JSON 字符串转换为 NLua 明确支持的类型。
        /// </summary>
        public static object? ConvertJsonToLuaCompatible(string json, LuaLogBridge logger)
        {
            try
            {
                var node = JsonNode.Parse(json);
                return ConvertNode(node);
            }
            catch (Exception ex)
            {
                logger.error($"JSON 解析或转换失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 递归转换 JsonNode
        /// </summary>
        private static object? ConvertNode(JsonNode? node)
        {
            if (node is null) return null;

            if (node is JsonObject obj)
            {
                var dict = new Dictionary<string, object?>();
                foreach ((string key, var value) in obj)
                {
                    dict[key] = ConvertNode(value);
                }
                return dict;
            }

            if (node is JsonArray arr)
            {
                var list = new List<object?>();
                foreach (var item in arr)
                {
                    list.Add(ConvertNode(item));
                }
                return list;
            }

            if (node is JsonValue val)
            {
                // 从 JsonValue 中提取基础类型
                return val.TryGetValue<object>(out object? result) ? result : null;
            }

            return null;
        }
    }

    /// <summary>
    /// 向 Lua 暴露安全的正则表达式功能。
    /// </summary>
    private class LuaRegexBridge(LuaLogBridge logger)
    {
        private LuaLogBridge Logger { get; } = logger;
        public object is_match(string input, string pattern)
        {
            try { return Regex.IsMatch(input, pattern); }
            catch (Exception ex) { Logger.error($"regex.is_match 失败: {ex.Message}"); return false; }
        }
        public string? match(string input, string pattern)
        {
            try { return Regex.Match(input, pattern).Value; }
            catch (Exception ex) { Logger.error($"regex.match 失败: {ex.Message}"); return null; }
        }
        public object? match_all(string input, string pattern)
        {
            try { return Regex.Matches(input, pattern).Select(m => m.Value).ToList(); }
            catch (Exception ex) { Logger.error($"regex.match_all 失败: {ex.Message}"); return null; }
        }
    }
    
    /// <summary>
    /// 向 Lua 暴露一个安全的日志记录器。
    /// </summary>
    private class LuaLogBridge(LuaScriptModuleProcessorDebugDto debugDto)
    {
        private LuaScriptModuleProcessorDebugDto DebugDto { get; } = debugDto;

        // ReSharper disable once InconsistentNaming
        public void info(string message) => this.DebugDto.Logs.Add($"[INFO] {message}");

        // ReSharper disable once InconsistentNaming
        public void warn(string message) => this.DebugDto.Logs.Add($"[WARN] {message}");

        // ReSharper disable once InconsistentNaming
        public void error(string message) => this.DebugDto.Logs.Add($"[ERROR] {message}");
    }

    /// <summary>
    /// Lua 脚本模块处理器的调试数据传输对象。
    /// </summary>
    internal class LuaScriptModuleProcessorDebugDto : IModuleProcessorDebugDto
    {
        /// <summary>
        /// 实际执行的 Lua 脚本内容。
        /// </summary>
        public string? ExecutedScript { get; set; }

        /// <summary>
        /// 脚本执行期间发生的运行时错误（如果有）。
        /// </summary>
        public string? RuntimeError { get; set; }

        /// <summary>
        /// 脚本通过 log 模块输出的日志。
        /// </summary>
        public List<string> Logs { get; } = new();
    }
}