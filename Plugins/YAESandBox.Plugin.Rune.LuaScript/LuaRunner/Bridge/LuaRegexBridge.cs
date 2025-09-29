using System.Text.Json;
using System.Text.RegularExpressions;
using NLua;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;

#pragma warning disable CS8974 // 将方法组转换为非委托类型

// ReSharper disable InconsistentNaming
namespace YAESandBox.Plugin.Rune.LuaScript.LuaRunner.Bridge;

/// <summary>
/// 向 Lua 暴露安全的正则表达式功能。
/// </summary>
public class LuaRegexBridge : ILuaBridge
{
    /// <summary>
    /// 从 Lua table 解析 C# RegexOptions。
    /// </summary>
    private static RegexOptions ParseOptions(LuaTable? optionsTable)
    {
        var options = RegexOptions.None;
        if (optionsTable == null) return options;

        // 检查布尔选项
        if (optionsTable["ignore_case"] is true) options |= RegexOptions.IgnoreCase;
        if (optionsTable["multiline"] is true) options |= RegexOptions.Multiline;
        if (optionsTable["dotall"] is true) options |= RegexOptions.Singleline;

        // 默认启用编译以提高性能
        options |= RegexOptions.Compiled;

        return options;
    }

    /// <summary>
    /// 辅助方法，用于将C#对象转换为Lua Table。
    /// </summary>
    private static object? ConvertObjectToLuaTable(object csharpObject, Lua luaState, LuaLogBridge logger, string callingMethodName)
    {
        try
        {
            // 1. 将 C# 对象序列化为 JSON 字符串
            string jsonString = JsonSerializer.Serialize(csharpObject, YaeSandBoxJsonHelper.JsonSerializerOptions);

            // 2. 获取 Lua 中的 json.decode 函数
            if (luaState["json.decode"] is not LuaFunction jsonDecodeFunc)
            {
                logger.error($"在 Lua 环境中找不到 'json.decode' 函数。无法在 {callingMethodName} 中转换结果。");
                return null; // 返回 nil
            }

            // 3. 调用 Lua 函数，将 JSON 字符串转换为 Lua Table
            object[]? result = jsonDecodeFunc.Call(jsonString);

            // 4. Call 返回的是一个 object[]，取第一个元素即为我们的 Lua Table
            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.error($"{callingMethodName} 转换结果失败。{ex.ToFormattedString()}");
            return null;
        }
    }

    /// <summary>
    /// 是否匹配成功
    /// </summary>
    private static object is_match(string input, string pattern, LuaTable? options, LuaLogBridge logger)
    {
        try
        {
            return Regex.IsMatch(input, pattern, ParseOptions(options));
        }
        catch (Exception ex)
        {
            logger.error($"regex.is_match 失败。{ex.ToFormattedString()}");
            return false;
        }
    }

    /// <summary>
    /// 查找第一个匹配项，并返回包含所有捕获组的 table。
    /// </summary>
    private static object? match(string input, string pattern, LuaTable? options, Lua luaState, LuaLogBridge logger)
    {
        try
        {
            var matchResult = Regex.Match(input, pattern, ParseOptions(options));
            if (!matchResult.Success)
            {
                return null;
            }

            // 返回所有捕获组的列表，[1]是完整匹配，[2]是第一个捕获组，以此类推。
            var groupsList = matchResult.Groups.Cast<Group>().Select(g => g.Value).ToList();
            return ConvertObjectToLuaTable(groupsList, luaState, logger, "regex.match");
        }
        catch (Exception ex)
        {
            logger.error($"regex.match 失败。{ex.ToFormattedString()}");
            return null;
        }
    }

    /// <summary>
    /// 查找所有匹配项，并返回一个 table 的 table，每个子 table 包含一个匹配及其所有捕获组。
    /// </summary>
    private static object? match_all(string input, string pattern, LuaTable? options, Lua luaState, LuaLogBridge logger)
    {
        try
        {
            var matches = Regex.Matches(input, pattern, ParseOptions(options));
            var allMatchesList = matches
                .Select(m => m.Groups.Cast<Group>().Select(g => g.Value).ToList())
                .ToList();
            return ConvertObjectToLuaTable(allMatchesList, luaState, logger, "regex.match_all");
        }
        catch (Exception ex)
        {
            logger.error($"regex.match_all 失败。{ex.ToFormattedString()}");
            return null;
        }
    }

    /// <summary>
    /// 在输入字符串中查找并替换匹配项。
    /// </summary>
    private static string? replace(string input, string pattern, string replacement, LuaTable? options, LuaLogBridge logger)
    {
        try
        {
            var regexOptions = ParseOptions(options);
            var regex = new Regex(pattern, regexOptions, TimeSpan.FromSeconds(5));

            object? countObj = options?["count"];
            int countLimit = 0;

            if (countObj != null)
            {
                try
                {
                    // Convert.ToInt32 可以处理 int, long, double, string 等多种类型，非常灵活
                    countLimit = Convert.ToInt32(countObj);
                }
                catch (Exception ex) when (ex is FormatException or OverflowException)
                {
                    // 如果用户提供了一个无法转换为整数的值（如 "abc"），则记录警告并忽略该选项
                    logger.warn($"regex.replace 的 'count' 选项值 ('{countObj}') 无效，已忽略。{ex.ToFormattedString()}");
                    countLimit = 0; // 重置为不限制
                }
            }

            // 如果解析出的限制次数大于0，则使用带 count 参数的 Replace 重载方法
            if (countLimit > 0)
            {
                return regex.Replace(input, replacement, countLimit);
            }

            return regex.Replace(input, replacement);
        }
        catch (Exception ex)
        {
            logger.error($"regex.replace 失败。{ex.ToFormattedString()}");
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
            // --- is_match ---
    // 重载1: 接受 options table
    regexTable["is_match"] = (string input, string pattern, LuaTable? options) => 
        is_match(input, pattern, options, logger);
    // 重载2: 不接受 options table，自动传入 null
    regexTable["is_match_no_options"] = (string input, string pattern) => 
        is_match(input, pattern, null, logger);
    // 使用一个小的 Lua shim 来允许多个签名
    luaState.DoString(@"
        local original_is_match = regex.is_match
        local no_options_is_match = regex.is_match_no_options
        regex.is_match_no_options = nil -- 清理临时函数
        regex.is_match = function(input, pattern, options)
            if options == nil then
                return no_options_is_match(input, pattern)
            else
                return original_is_match(input, pattern, options)
            end
        end
    ");


    // --- match ---
    // 重载1: 接受 options table
    regexTable["match"] = (string input, string pattern, LuaTable? options) => 
        match(input, pattern, options, luaState, logger);
    // 重载2: 不接受 options table，自动传入 null
    regexTable["match_no_options"] = (string input, string pattern) => 
        match(input, pattern, null, luaState, logger);
    luaState.DoString(@"
        local original_match = regex.match
        local no_options_match = regex.match_no_options
        regex.match_no_options = nil
        regex.match = function(input, pattern, options)
            if options == nil then
                return no_options_match(input, pattern)
            else
                return original_match(input, pattern, options)
            end
        end
    ");

    // --- match_all ---
    // 重载1: 接受 options table
    regexTable["match_all"] = (string input, string pattern, LuaTable? options) => 
        match_all(input, pattern, options, luaState, logger);
    // 重载2: 不接受 options table，自动传入 null
    regexTable["match_all_no_options"] = (string input, string pattern) => 
        match_all(input, pattern, null, luaState, logger);
    luaState.DoString(@"
        local original_match_all = regex.match_all
        local no_options_match_all = regex.match_all_no_options
        regex.match_all_no_options = nil
        regex.match_all = function(input, pattern, options)
            if options == nil then
                return no_options_match_all(input, pattern)
            else
                return original_match_all(input, pattern, options)
            end
        end
    ");

    // --- replace ---
    // 重载1: 接受 options table
    regexTable["replace"] = (string input, string pattern, string replacement, LuaTable? options) =>
        replace(input, pattern, replacement, options, logger);
    // 重载2: 不接受 options table，自动传入 null
    regexTable["replace_no_options"] = (string input, string pattern, string replacement) =>
        replace(input, pattern, replacement, null, logger);
    luaState.DoString(@"
        local original_replace = regex.replace
        local no_options_replace = regex.replace_no_options
        regex.replace_no_options = nil
        regex.replace = function(input, pattern, replacement, options)
            if options == nil then
                return no_options_replace(input, pattern, replacement)
            else
                return original_replace(input, pattern, replacement, options)
            end
        end
    ");
    }
}