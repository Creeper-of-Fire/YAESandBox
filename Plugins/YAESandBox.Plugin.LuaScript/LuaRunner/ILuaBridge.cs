using NLua;
using YAESandBox.Plugin.Rune.LuaScript.LuaRunner.Bridge;

namespace YAESandBox.Plugin.Rune.LuaScript.LuaRunner;

/// <summary>
/// 定义了一个可以被注册到 Lua 运行环境中的功能桥的接口。
/// </summary>
public interface ILuaBridge
{
    /// <summary>
    /// 获取此功能桥在 Lua 中注册的全局表名 (例如 "ctx", "regex")。
    /// </summary>
    string BridgeName { get; }

    /// <summary>
    /// 将此桥的功能注册到提供的 Lua 状态中。
    /// </summary>
    /// <param name="luaState">要注册到的 NLua.Lua 实例。</param>
    /// <param name="logger">一个共享的日志记录器，供桥在内部使用。</param>
    void Register(Lua luaState, LuaLogBridge logger);
}