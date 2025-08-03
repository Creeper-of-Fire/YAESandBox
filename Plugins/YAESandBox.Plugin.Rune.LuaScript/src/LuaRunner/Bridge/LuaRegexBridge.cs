using System.Text.RegularExpressions;
using NLua;

#pragma warning disable CS8974 // 将方法组转换为非委托类型

// ReSharper disable InconsistentNaming
namespace YAESandBox.Plugin.LuaScript.LuaRunner.Bridge;

/// <summary>
/// 向 Lua 暴露安全的正则表达式功能。
/// </summary>
public class LuaRegexBridge : ILuaBridge
{
    private static object is_match(string input, string pattern, LuaLogBridge logger)
    {
        try
        {
            return Regex.IsMatch(input, pattern);
        }
        catch (Exception ex)
        {
            logger.error($"regex.is_match 失败: {ex.Message}");
            return false;
        }
    }

    private static string? match(string input, string pattern, LuaLogBridge logger)
    {
        try
        {
            return Regex.Match(input, pattern).Value;
        }
        catch (Exception ex)
        {
            logger.error($"regex.match 失败: {ex.Message}");
            return null;
        }
    }

    private static object? match_all(string input, string pattern, LuaLogBridge logger)
    {
        try
        {
            return Regex.Matches(input, pattern).Select(m => m.Value).ToList();
        }
        catch (Exception ex)
        {
            logger.error($"regex.match_all 失败: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public string BridgeName => "regex";

    /// <inheritdoc />
    public void Register(Lua luaState, LuaLogBridge logger)
    {
        // 注册 regex.*
        luaState.NewTable(this.BridgeName);
        var regexTable = (LuaTable)luaState[this.BridgeName];
        regexTable["is_match"] = (string input, string pattern) => is_match(input, pattern, logger);
        regexTable["match"] = (string input, string pattern) => match(input, pattern, logger);
        regexTable["match_all"] = (string input, string pattern) => match_all(input, pattern, logger);
    }
}