using NLua;
using YamlDotNet.Serialization; // 引入 YamlDotNet
using YamlDotNet.Serialization.NamingConventions;
using YAESandBox.Depend.Results;
using YamlDotNet.Core;

// ReSharper disable InconsistentNaming
namespace YAESandBox.Plugin.LuaScript.LuaRunner.Bridge;

/// <summary>
/// 向 Lua 暴露一个由 C# YamlDotNet 库驱动的高性能 YAML 解析器。
/// </summary>
public class LuaYamlBridge : ILuaBridge
{
    /// <inheritdoc />
    public string BridgeName => "yaml";

    // YamlDotNet Deserializer 是线程安全的，可以静态创建以获得最佳性能
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance) // 可根据需要选择命名约定
        .Build();

    /// <summary>
    /// 解析 YAML 字符串并返回一个 Lua table。
    /// YamlDotNet 会将 YAML 解析为 ExpandoObject 或 Dictionary&lt;object, object&gt;，
    /// NLua 可以自动将其转换为 Lua table。
    /// </summary>
    private static object? decode(string yamlString, Lua luaState, LuaLogBridge logger)
    {
        try
        {
            // 将 YAML 解析为通用的 object，通常是 Dictionary<object, object>
            // 这对于 NLua 的转换非常友好
            object resultObject = YamlDeserializer.Deserialize<object>(yamlString);
            return LuaBridgeHelper.ConvertCSharpObjectToLuaTable(resultObject, luaState, logger, "yaml.decode");
        }
        catch (YamlException ex)
        {
            // YamlDotNet 的异常信息非常详细
            logger.error($"YAML 解析失败: {ex.ToFormattedString()}");
            return null;
        }
        catch (Exception ex)
        {
            logger.error($"YAML 解析时发生意外错误。{ex.ToFormattedString()}");
            return null;
        }
    }

    // 如果需要，也可以实现 encode
    // private static readonly ISerializer YamlSerializer = ...

    /// <inheritdoc />
    public void Register(Lua luaState, LuaLogBridge logger)
    {
        luaState.NewTable(this.BridgeName);
        var yamlTable = (LuaTable)luaState[this.BridgeName];

        // 注册 decode 函数
        yamlTable["decode"] = (string yamlString) => decode(yamlString, luaState, logger);

        logger.info("YAML 解析桥 (C#/YamlDotNet) 加载成功。");
    }
}