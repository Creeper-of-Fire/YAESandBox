using System.Text.Json;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Step.StepProcessor;

namespace YAESandBox.Workflow.Module.ExactModule;

internal partial class LuaScriptModuleProcessor
{
    /// <summary>
    /// 作为 C# 与 Lua 脚本之间交互的安全桥梁。
    /// 仅暴露 Get 和 Set 方法，防止 Lua 脚本访问不应访问的内部状态。
    /// </summary>
    private class LuaContextBridge(StepProcessorContent stepContent)
    {
        private StepProcessorContent StepContent { get; } = stepContent;

        /// <summary>
        /// 从步骤变量池中获取一个变量。暴露给 Lua 使用。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <returns>变量的值，如果不存在则为 null。</returns>
        // ReSharper disable once InconsistentNaming
        public object? get(string name)
        {
            return this.StepContent.InputVar(name);
        }

        /// <summary>
        ///向步骤变量池中设置一个变量。暴露给 Lua 使用。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <param name="value">要设置的值。</param>
        // ReSharper disable once InconsistentNaming
        public void set(string name, object value)
        {
            this.StepContent.OutputVar(name, value);
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
    /// 向 Lua 暴露基于 System.Text.Json 的功能。
    /// </summary>
    private class LuaJsonBridge
    {
        // ReSharper disable once InconsistentNaming
        public string? encode(object? value)
        {
            if (value == null) return null;
            // NLua 会将 Lua table 转换为 Dictionary<object, object> 或 List<object>
            // System.Text.Json 可以很好地处理这些类型
            return JsonSerializer.Serialize(value, YaeSandBoxJsonHelper.JsonSerializerOptions);
        }

        // ReSharper disable once InconsistentNaming
        public object? decode(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            // 反序列化为 object，NLua 会自动处理将 C# 字典和列表转换为 Lua table
            return JsonSerializer.Deserialize<object>(json, YaeSandBoxJsonHelper.JsonSerializerOptions);
        }
    }

    /// <summary>
    /// 向 Lua 暴露安全的正则表达式功能。
    /// </summary>
    private class LuaRegexBridge
    {
        // ReSharper disable once InconsistentNaming
        public bool is_match(string input, string pattern)
        {
            return Regex.IsMatch(input, pattern);
        }

        // ReSharper disable once InconsistentNaming
        public string? match(string input, string pattern)
        {
            return Regex.Match(input, pattern).Value;
        }

        // ReSharper disable once InconsistentNaming
        public List<string> match_all(string input, string pattern)
        {
            return Regex.Matches(input, pattern).Select(m => m.Value).ToList();
        }
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