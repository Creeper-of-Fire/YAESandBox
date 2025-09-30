using System.Text.Json;
using NLua;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Tuum;

// ReSharper disable InconsistentNaming

namespace YAESandBox.Plugin.Rune.LuaScript.LuaRunner.Bridge;

/// <summary>
/// 作为 C# 与 Lua 脚本之间交互的安全桥梁。
/// 仅暴露 Get 和 Set 方法，防止 Lua 脚本访问不应访问的内部状态。
/// </summary>
public class LuaContextBridge(TuumProcessor.TuumProcessorContent tuumContent) : ILuaBridge
{
    private TuumProcessor.TuumProcessorContent TuumContent { get; } = tuumContent;

    /// <summary>
    /// 从枢机变量池中获取一个变量。
    /// </summary>
    /// <param name="name">变量名。</param>
    /// <param name="luaState"></param>
    /// <param name="logger"></param>
    /// <returns>变量的值，如果不存在则为 null。</returns>
    private object? get(string name, Lua luaState, LuaLogBridge logger)
    {
        try
        {
            object? rawValue = this.TuumContent.GetTuumVar(name);

            // 如果值是 null 或者已经是基础类型，直接返回
            if (rawValue is null or string or bool or double or int or long)
                return rawValue;

            // 将 C# 对象序列化为 JSON 字符串
            string jsonString = JsonSerializer.Serialize(rawValue, YaeSandBoxJsonHelper.JsonSerializerOptions);

            // 获取 Lua 中的 json.decode 函数
            if (luaState["json.decode"] is not LuaFunction jsonDecodeFunc)
            {
                logger.error("在 Lua 环境中找不到 'json.decode' 函数。无法转换 ctx 变量。");
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
            logger.error($"ctx.get('{name}') 失败。{ex.ToFormattedString()}");
            return null; // 向 Lua 返回 nil
        }
    }

    /// <summary>
    /// 向枢机变量池中设置一个变量。
    /// </summary>
    /// <param name="name">变量名。</param>
    /// <param name="value">要设置的值。</param>
    /// <param name="logger"></param>
    private void set(string name, object? value, LuaLogBridge logger)
    {
        try
        {
            // 在将变量存入 C# 上下文之前，将其从 Lua 对象深度转换为纯 C# 对象。
            object? csharpValue = LuaConverter.ConvertLuaToCSharp(value, logger);
            this.TuumContent.SetTuumVar(name, csharpValue);
        }
        catch (Exception ex)
        {
            logger.error($"ctx.set('{name}') 失败。{ex.ToFormattedString()}");
        }
    }

    /// <inheritdoc />
    public string BridgeName => "ctx";

    /// <inheritdoc />
    public void Register(Lua luaState, LuaLogBridge logger)
    {
        // 注册 ctx.*
        luaState.NewTable(this.BridgeName);
        var ctxTable = (LuaTable)luaState[this.BridgeName];
        ctxTable["get"] = (string name) => this.get(name, luaState, logger);
        ctxTable["set"] = (string name, object value) => this.set(name, value, logger);
    }
}