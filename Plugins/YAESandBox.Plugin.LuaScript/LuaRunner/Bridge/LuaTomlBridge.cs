using NLua;
using Tomlyn; // 引入 Tomlyn 命名空间
// 需要 TomlTable
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Utility;

// ReSharper disable InconsistentNaming
namespace YAESandBox.Plugin.LuaScript.LuaRunner.Bridge;

/// <summary>
/// 向 Lua 暴露一个由 C# Tomlyn 库驱动的高性能 TOML 解析器。
/// </summary>
public class LuaTomlBridge : ILuaBridge
{
    /// <inheritdoc />
    public string BridgeName => "toml";

    /// <summary>
    /// 解析 TOML 字符串并返回一个 Lua table。
    /// NLua 会自动将 C# 的 Dictionary 转换为 Lua table。
    /// </summary>
    private static object? decode(string tomlString, Lua luaState, LuaLogBridge logger)
    {
        try
        {
            string compliantTomlString = TOMLHelper.PreprocessUnquotedKeys(tomlString);
            // 如果预处理修改了字符串，可以记录日志以方便调试
            if (compliantTomlString != tomlString)
            {
                logger.info("TOML 预处理器已修复不合规的键。");
            }
            // Tomlyn.Toml.ToModel 将 TOML 字符串解析为 TomlTable，
            // 它本质上是一个 Dictionary<string, object>，NLua 可以轻松转换。
            var model = Toml.ToModel(compliantTomlString);
            return LuaBridgeHelper.ConvertCSharpObjectToLuaTable(model, luaState, logger, "toml.decode");
        }
        catch (TomlException ex)
        {
            // Tomlyn 会抛出带有详细位置信息的 TomlException，这对于调试非常有用。
            logger.error($"TOML 解析失败: {ex.ToFormattedString()}");
            return null; // 返回 nil 表示失败
        }
        catch (Exception ex)
        {
            logger.error($"TOML 解析时发生意外错误。{ex.ToFormattedString()}");
            return null;
        }
    }

    /// <inheritdoc />
    public void Register(Lua luaState, LuaLogBridge logger)
    {
        luaState.NewTable(this.BridgeName);
        var tomlTable = (LuaTable)luaState[this.BridgeName];

        // 注册 decode 函数
        tomlTable["decode"] = (string tomlString) => decode(tomlString, luaState, logger);

        // （可选）你也可以添加一个 encode 函数，如果 Lua 脚本需要将 table 转换回 TOML 字符串
        // tomlTable["encode"] = (LuaTable luaTbl) => encode(luaTbl, logger);

        logger.info("TOML 解析桥 (C#/Tomlyn) 加载成功。");
    }
}