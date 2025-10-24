using NLua;
using YAESandBox.Workflow.Core.DebugDto;

// ReSharper disable InconsistentNaming
namespace YAESandBox.Plugin.LuaScript.LuaRunner.Bridge;

/// <summary>
/// 向 Lua 暴露一个安全的日志记录器。
/// </summary>
public class LuaLogBridge(IDebugDtoWithLogs debugDto)
{
    private IDebugDtoWithLogs DebugDto { get; } = debugDto;
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    public void info(string message) => this.DebugDto.Logs.Add($"[INFO] {message}");

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    public void warn(string message) => this.DebugDto.Logs.Add($"[WARN] {message}");
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    public void error(string message) => this.DebugDto.Logs.Add($"[ERROR] {message}");

    private static string BridgeName => "log";

    /// <summary>
    /// 注册
    /// </summary>
    /// <param name="luaState"></param>
    /// <param name="logger"></param>
    public void Register(Lua luaState, LuaLogBridge logger)
    {
        luaState.NewTable(BridgeName);
        var logTable = (LuaTable)luaState[BridgeName];
        logTable["info"] = (Action<string>)this.info;
        logTable["warn"] = (Action<string>)this.warn;
        logTable["error"] = (Action<string>)this.error;
    }
}