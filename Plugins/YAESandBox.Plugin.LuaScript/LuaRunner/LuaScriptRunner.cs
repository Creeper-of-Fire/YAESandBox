using System.Text;
using NLua;
using YAESandBox.Depend.Results;
using YAESandBox.Plugin.LuaScript.LuaRunner.Bridge;
using YAESandBox.Workflow.Core.DebugDto;

#pragma warning disable CS8974 // 将方法组转换为非委托类型

namespace YAESandBox.Plugin.LuaScript.LuaRunner;

/// <summary>
/// 通用的 Lua 脚本执行器。
/// 负责创建安全的沙箱环境、注入标准的 API 桥并执行脚本。
/// </summary>
/// <param name="debugDto">用于记录日志和错误的调试对象。</param>
/// <param name="bridges"></param>
public class LuaScriptRunner(IDebugDtoWithLogs debugDto, IEnumerable<ILuaBridge> bridges)
{
    private IDebugDtoWithLogs DebugDto { get; } = debugDto;
    private IEnumerable<ILuaBridge> Bridges { get; } = bridges;

    /// <summary>
    /// 异步执行提供的 Lua 脚本。
    /// </summary>
    /// <param name="script">要执行的 Lua 脚本字符串。</param>
    /// <param name="preExecutionSetup">在执行脚本之前，对 Lua 环境进行额外设置的委托。可用于注入特定于调用的变量。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果。</returns>
    public Task<Result> ExecuteAsync(string script, Action<Lua>? preExecutionSetup = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return Result.Ok().AsCompletedTask();
        }

        try
        {
            using var lua = new Lua();
            lua.State.Encoding = Encoding.UTF8;

            // lua.LoadCLRPackage();

            // --- 沙箱化：移除危险的内建符文 ---
            lua.DoString(
                """
                os = nil; 
                io = nil; 
                debug = nil; 
                package = nil;
                dofile = nil; 
                loadfile = nil; 
                require = nil;
                """
            );


            // --- 2. 准备核心服务：日志记录器 ---
            // 日志是基础服务，所有桥都可能需要它，所以我们首先创建它。
            var logger = new LuaLogBridge(this.DebugDto);
            logger.Register(lua, logger); // 日志桥自己注册自己

            // --- 3. 注册所有通过构建器添加的功能桥 ---
            foreach (var bridge in this.Bridges)
            {
                try
                {
                    bridge.Register(lua, logger);
                }
                catch (Exception ex)
                {
                    logger.error($"注册功能桥 '{bridge.BridgeName}' 时失败。{ex.ToFormattedString()}");
                    // 注册失败时，可以选择继续或中止，这里我们选择记录错误并继续
                }
            }

            // --- 执行特定于调用的设置 ---
            preExecutionSetup?.Invoke(lua);

            // --- 执行主脚本 ---
            lua.DoString(script);

            return Result.Ok().AsCompletedTask();
        }
        catch (Exception ex)
        {
            var primaryException = ex.InnerException ?? ex;
            string conciseErrorMessage = $"执行 Lua 脚本时发生错误: [{primaryException.GetType().Name}] {primaryException.Message}";
            string fullErrorDetails = ex.ToString();

            if (this.DebugDto is { } loggableDto)
            {
                loggableDto.RuntimeError = conciseErrorMessage;
                loggableDto.Logs.Add("[FATAL] 脚本执行异常. 详细信息如下:");
                using var reader = new StringReader(fullErrorDetails);
                for (string? line = reader.ReadLine(); line is not null; line = reader.ReadLine())
                {
                    loggableDto.Logs.Add(line);
                }
            }

            var builder = new StringBuilder();
            foreach (string log in this.DebugDto.Logs)
            {
                builder.AppendLine(log + "\n");
            }

            return Result.Fail(fullErrorDetails + "\n" + builder,ex).AsCompletedTask();
        }
    }
}

/// <summary>
/// 包含 C# 和 Lua 之间数据转换的辅助方法。
/// </summary>
public static class LuaConverter
{
    /// <summary>
    /// 将从 Lua 传入的 object (可能是 LuaTable) 递归转换为纯粹的 C# 对象。
    /// 这是为了切断对象与已销毁的 Lua 状态的联系。
    /// </summary>
    public static object? ConvertLuaToCSharp(object? luaObject, LuaLogBridge logger)
    {
        switch (luaObject)
        {
            case null:
                return null;

            case LuaFunction:
                logger.warn("无法持久化或序列化 Lua 函数。该值将被忽略 (视为 nil)。");
                return null;

            case LuaTable table:
            {
                // NLua 的 table 可能表现为类数组或类字典，需要区分处理
                var dict = new Dictionary<object, object?>();
                var list = new List<object?>();
                bool isList = true;

                foreach (object? key in table.Keys)
                {
                    // 键和值都需要递归转换
                    object? convertedKey = ConvertLuaToCSharp(key, logger);
                    object? convertedValue = ConvertLuaToCSharp(table[key], logger);

                    if (convertedKey is null) continue; // 不支持 nil 键

                    dict[convertedKey] = convertedValue;

                    // 检查是否能构成一个连续的、从1开始的整数索引数组 (Lua 数组的特征)
                    if (isList && key is long numKey && numKey == list.Count + 1)
                    {
                        list.Add(convertedValue);
                    }
                    else
                    {
                        isList = false;
                    }
                }

                // 如果所有键正好构成了 1, 2, 3... 的序列，就返回 List<T>
                return isList && list.Count == dict.Count ? list : dict;
            }

            // 对于基础类型 (string, double, bool, long等)，直接返回
            case string or double or bool or long:
                return luaObject;

            // 其他 C# 对象（如 LuaDateTimeObject）如果被传回，也直接返回
            default:
                return luaObject;
        }
    }
}