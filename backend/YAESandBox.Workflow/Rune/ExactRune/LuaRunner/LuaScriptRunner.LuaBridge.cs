using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NLua;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Step.StepProcessor;

// ReSharper disable InconsistentNaming

namespace YAESandBox.Workflow.Rune.ExactRune.LuaRunner;

internal partial class LuaScriptRunner
{
    /// <summary>
    /// 作为 C# 与 Lua 脚本之间交互的安全桥梁。
    /// 仅暴露 Get 和 Set 方法，防止 Lua 脚本访问不应访问的内部状态。
    /// </summary>
    private class LuaContextBridge(StepProcessorContent stepContent, LuaLogBridge logger, Lua luaState)
    {
        private StepProcessorContent StepContent { get; } = stepContent;
        private LuaLogBridge Logger { get; } = logger;
        private Lua LuaState { get; } = luaState;

        /// <summary>
        /// 从步骤变量池中获取一个变量。暴露给 Lua 使用。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <returns>变量的值，如果不存在则为 null。</returns>
        public object? get(string name)
        {
            try
            {
                object? rawValue = this.StepContent.InputVar(name);

                // 如果值是 null 或者已经是基础类型，直接返回
                if (rawValue is null or string or bool or double or int or long)
                    return rawValue;

                // 将 C# 对象序列化为 JSON 字符串
                string jsonString = JsonSerializer.Serialize(rawValue, YaeSandBoxJsonHelper.JsonSerializerOptions);

                // 获取 Lua 中的 json.decode 函数
                if (this.LuaState["json.decode"] is not LuaFunction jsonDecodeFunc)
                {
                    this.Logger.error("在 Lua 环境中找不到 'json.decode' 函数。无法转换 ctx 变量。");
                    return null; // 返回 nil
                }

                // 调用 Lua 函数，将 JSON 字符串转换为 Lua Table
                object[]? result = jsonDecodeFunc.Call(jsonString);

                // Call 返回的是一个 object[]，取第一个元素
                object? gotItem = result.FirstOrDefault();
                return gotItem;
            }
            catch (Exception ex)
            {
                this.Logger.error($"ctx.get('{name}') 失败: {ex.Message}");
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
                // 在将变量存入 C# 上下文之前，将其从 Lua 对象深度转换为纯 C# 对象。
                object? csharpValue = LuaConverter.ConvertLuaToCSharp(value, this.Logger);
                this.StepContent.OutputVar(name, csharpValue);
            }
            catch (Exception ex)
            {
                this.Logger.error($"ctx.set('{name}') 失败: {ex.Message}");
            }
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
            try
            {
                return Regex.IsMatch(input, pattern);
            }
            catch (Exception ex)
            {
                this.Logger.error($"regex.is_match 失败: {ex.Message}");
                return false;
            }
        }

        public string? match(string input, string pattern)
        {
            try
            {
                return Regex.Match(input, pattern).Value;
            }
            catch (Exception ex)
            {
                this.Logger.error($"regex.match 失败: {ex.Message}");
                return null;
            }
        }

        public object? match_all(string input, string pattern)
        {
            try
            {
                return Regex.Matches(input, pattern).Select(m => m.Value).ToList();
            }
            catch (Exception ex)
            {
                this.Logger.error($"regex.match_all 失败: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 向 Lua 暴露一个安全的日志记录器。
    /// </summary>
    private class LuaLogBridge(ILogsDebugDto debugDto)
    {
        private ILogsDebugDto DebugDto { get; } = debugDto;

        // ReSharper disable once InconsistentNaming
        public void info(string message) => this.DebugDto.Logs.Add($"[INFO] {message}");

        // ReSharper disable once InconsistentNaming
        public void warn(string message) => this.DebugDto.Logs.Add($"[WARN] {message}");

        // ReSharper disable once InconsistentNaming
        public void error(string message) => this.DebugDto.Logs.Add($"[ERROR] {message}");
    }
}