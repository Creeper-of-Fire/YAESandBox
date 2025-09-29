using System.Reflection;
using NLua;

// ReSharper disable InconsistentNaming
namespace YAESandBox.Plugin.Rune.LuaScript.LuaRunner.Bridge;

/// <summary>
/// 一个功能桥，负责向 Lua 环境中注入一个功能完备的 JSON 库。
/// 它会尝试加载嵌入的 `json.lua`，如果失败，则回退到一个简易的内置实现。
/// </summary>
public class LuaJsonBridge : ILuaBridge
{
    /// <inheritdoc />
    public string BridgeName => "json";

    /// <inheritdoc />
    public void Register(Lua luaState, LuaLogBridge logger)
    {
        try
        {
            string jsonLuaCode = LoadEmbeddedScript("json.lua");

            luaState.DoString($"json = (function() {jsonLuaCode} end)()");

            // 验证加载
            luaState.DoString(@"
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
            luaState.DoString(
                """
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
                """
            );
        }
    }

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
            .SingleOrDefault(str => str.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));

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
}