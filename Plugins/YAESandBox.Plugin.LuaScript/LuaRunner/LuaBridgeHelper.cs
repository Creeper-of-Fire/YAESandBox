using NLua;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using YAESandBox.Plugin.LuaScript.LuaRunner.Bridge;

namespace YAESandBox.Plugin.LuaScript.LuaRunner;

/// <summary>
/// 为 ILuaBridge 实现提供通用的辅助方法。
/// </summary>
public static class LuaBridgeHelper
{
    /// <summary>
    /// 将一个 C# 对象转换为一个纯粹的 Lua Table，以避免 "userdata" 问题。
    /// 该方法通过 JSON 序列化和 Lua 的 json.decode 函数作为桥梁。
    /// </summary>
    /// <param name="csharpObject">要转换的 C# 对象。</param>
    /// <param name="luaState">当前的 NLua.Lua 实例。</param>
    /// <param name="logger">用于记录错误的日志桥。</param>
    /// <param name="context">描述调用上下文的字符串，用于生成更清晰的错误日志（例如 "in regex.match"）。</param>
    /// <returns>一个纯 Lua Table，如果转换失败则返回 null。</returns>
    public static object? ConvertCSharpObjectToLuaTable(object? csharpObject, Lua luaState, LuaLogBridge logger, string context)
    {
        if (csharpObject == null)
        {
            return null;
        }

        try
        {
            // 步骤 1: 将 C# 对象序列化为 JSON 字符串
            string jsonString = YaeSandBoxJsonHelper.Serialize(csharpObject);

            // 步骤 2: 获取 Lua 中的 json.decode 函数
            if (luaState["json.decode"] is not LuaFunction jsonDecodeFunc)
            {
                logger.error($"[{context}] 找不到 Lua 函数 'json.decode'，无法转换结果。");
                return null;
            }

            // 步骤 3: 调用 Lua 函数，将 JSON 字符串转换为一个纯 Lua Table
            object[]? result = jsonDecodeFunc.Call(jsonString);

            // 步骤 4: Call 返回的是一个 object[]，取第一个元素即为我们的 Lua Table
            return result?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.error($"[{context}] 将 C# 对象转换为 Lua 表失败。{ex.ToFormattedString()}");
            return null;
        }
    }
}