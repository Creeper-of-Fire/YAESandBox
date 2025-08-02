using System.Reflection;
using System.Text;
using NLua;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Step.StepProcessor;

#pragma warning disable CS8974 // 将方法组转换为非委托类型

namespace YAESandBox.Workflow.Rune.ExactRune.LuaRunner;

/// <summary>
/// 通用的 Lua 脚本执行器。
/// 负责创建安全的沙箱环境、注入标准的 API 桥并执行脚本。
/// </summary>
/// <param name="stepProcessorContent">当前步骤的执行上下文，用于访问变量。</param>
/// <param name="debugDto">用于记录日志和错误的调试对象。</param>
internal partial class LuaScriptRunner(StepProcessorContent stepProcessorContent, ILogsDebugDto debugDto)
{
    /// <summary>
    /// 从当前程序集的嵌入式资源中加载脚本文件，无需硬编码完整路径。
    /// </summary>
    /// <param name="fileName">要查找的文件名，例如 "json.lua"。</param>
    /// <returns>文件的文本内容。</returns>
    /// <exception cref="FileNotFoundException">如果找不到唯一的匹配资源。</exception>
    private static string LoadEmbeddedScript(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 资源名称的格式是 [默认命名空间].[文件夹路径].[文件名]
        // 我们通过查找以 ".fileName" 结尾的资源来定位它，这样更可靠。
        string? resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(str => str.EndsWith("." + fileName, StringComparison.Ordinal));

        if (string.IsNullOrEmpty(resourceName))
        {
            // 提供一个非常友好的错误信息，帮助调试
            string availableResources = string.Join("\n - ", assembly.GetManifestResourceNames());
            throw new FileNotFoundException(
                $"无法在嵌入式资源中找到唯一的文件 '{fileName}'。请确保：\n" +
                "1. 文件已包含在项目中。\n" +
                "2. 文件的“生成操作”已设置为“嵌入的资源”。\n" +
                "3. 项目中没有其他同名文件导致冲突。\n\n" +
                $"当前程序集中可用的资源有:\n - {availableResources}");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new FileNotFoundException($"无法加载资源流: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

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
            return Task.FromResult(Result.Ok());
        }

        try
        {
            using var lua = new Lua();
            lua.State.Encoding = Encoding.UTF8;

            lua.LoadCLRPackage();

            // --- 沙箱化：移除危险的内建符文 ---
            lua.DoString(@"
                os = nil; 
                io = nil; 
                debug = nil; 
                package = nil;
                dofile = nil; 
                loadfile = nil; 
                require = nil;
            ");


            // --- 注入标准 API 符文 ---
            var logger = new LuaLogBridge(debugDto);
            // 注册 log.*
            lua.NewTable("log");
            var logTable = (LuaTable)lua["log"];
            logTable["info"] = logger.info;
            logTable["warn"] = logger.warn;
            logTable["error"] = logger.error;

            try
            {
                string jsonLuaCode = LoadEmbeddedScript("json.lua");

                lua.DoString($"json = (function() {jsonLuaCode} end)()");

                // 验证加载
                lua.DoString(@"
                    if type(json) == 'table' and type(json.decode) == 'function' then
                        log.info('JSON库加载成功')
                    else
                        error('JSON库结构不正确')
                    end
                ");
            }
            catch (Exception ex)
            {
                logger.error($"加载JSON库失败: {ex.Message}");

                // 回退到内置简单实现
                lua.DoString(@"
                    json = {
                        decode = function(str)
                            if str == 'null' then return nil end
                            local fn, err = load('return '..str)
                            if not fn then 
                                log.error('JSON解析错误: '..err)
                                return nil 
                            end
                            return fn()
                        end,
                        encode = function(tbl)
                            return tostring(tbl) -- 简化实现
                        end
                    }
                    log.warn('使用内置简易JSON解析器')
                ");
            }

            var contextBridge = new LuaContextBridge(stepProcessorContent, logger, lua);
            var regexBridge = new LuaRegexBridge(logger);
            var dateTimeBridge = new LuaDateTimeBridge(logger);


            // 注册 ctx.*
            lua.NewTable("ctx");
            var ctxTable = (LuaTable)lua["ctx"];
            ctxTable["get"] = contextBridge.get;
            ctxTable["set"] = contextBridge.set;

            // 注册 regex.*
            lua.NewTable("regex");
            var regexTable = (LuaTable)lua["regex"];
            regexTable["is_match"] = regexBridge.is_match;
            regexTable["match"] = regexBridge.match;
            regexTable["match_all"] = regexBridge.match_all;

            // 注册 datetime.*
            lua.NewTable("datetime");
            var datetimeTable = (LuaTable)lua["datetime"];
            datetimeTable["utcnow"] = dateTimeBridge.utcnow;
            datetimeTable["now"] = dateTimeBridge.now;
            datetimeTable["parse"] = dateTimeBridge.parse;

            // --- 执行特定于调用的设置 ---
            preExecutionSetup?.Invoke(lua);

            // --- 执行主脚本 ---
            lua.DoString(script);

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            var primaryException = ex.InnerException ?? ex;
            string conciseErrorMessage = $"执行 Lua 脚本时发生错误: [{primaryException.GetType().Name}] {primaryException.Message}";
            string fullErrorDetails = ex.ToString();

            if (debugDto is { } loggableDto)
            {
                loggableDto.RuntimeError = conciseErrorMessage;
                loggableDto.Logs.Add($"[FATAL] 脚本执行异常. 详细信息如下:");
                using var reader = new StringReader(fullErrorDetails);
                for (string? line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    loggableDto.Logs.Add(line);
                }
            }

            var builder = new StringBuilder();
            foreach (var log in debugDto.Logs)
            {
                builder.AppendLine(log + "\n");
            }

            return Task.FromResult(Result.Fail(fullErrorDetails + "\n" + builder).ToResult());
        }
    }

    /// <summary>
    /// 包含 C# 和 Lua 之间数据转换的辅助方法。
    /// </summary>
    private static class LuaConverter
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

                    foreach (var key in table.Keys)
                    {
                        // 键和值都需要递归转换
                        var convertedKey = ConvertLuaToCSharp(key, logger);
                        var convertedValue = ConvertLuaToCSharp(table[key], logger);

                        if (convertedKey == null) continue; // 不支持 nil 键

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
}