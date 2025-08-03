using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Plugin.LuaScript.LuaRunner;

/// <summary>
/// 一个用于构建和配置 LuaScriptRunner 的构建器。
/// 它允许按需添加不同的功能桥，实现了灵活的功能组合。
/// </summary>
public class LuaRunnerBuilder(TuumProcessorContent tuumContent, IDebugDtoWithLogs debugDto)
{
    private List<ILuaBridge> Bridges { get; } = [];
    private TuumProcessorContent TuumContent { get; } = tuumContent;
    private IDebugDtoWithLogs DebugDto { get; } = debugDto;

    /// <summary>
    /// 向运行器中添加一个标准的功能桥。
    /// </summary>
    /// <param name="bridge">要添加的功能桥实例。</param>
    /// <returns>返回构建器本身，以支持链式调用。</returns>
    public LuaRunnerBuilder AddBridge(ILuaBridge bridge)
    {
        this.Bridges.Add(bridge);
        return this;
    }

    /// <summary>
    /// 根据当前配置构建一个 LuaScriptRunner 实例。
    /// </summary>
    /// <returns>一个配置完成的 LuaScriptRunner。</returns>
    public LuaScriptRunner Build()
    {
        return new LuaScriptRunner(this.TuumContent, this.DebugDto, this.Bridges);
    }
}