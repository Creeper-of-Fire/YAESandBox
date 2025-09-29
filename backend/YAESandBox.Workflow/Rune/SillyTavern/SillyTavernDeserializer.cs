using System.Text.Json;
using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.Rune.SillyTavern;

/// <summary>
/// 提供将 JSON 字符串安全地反序列化为 SillyTavern 模型的静态方法。
/// </summary>
public static class SillyTavernDeserializer
{
    /// <summary>
    /// 尝试将一个 JSON 字符串反序列化为 SillyTavernPreset 对象。
    /// </summary>
    /// <param name="jsonContent">包含预设数据的 JSON 字符串。</param>
    /// <returns>
    /// 如果成功，返回一个包含 <see cref="SillyTavernPreset"/> 实例的 Result.Ok；
    /// 如果失败，返回一个包含中文错误信息的 Result.Fail。
    /// </returns>
    public static Result<SillyTavernPreset> DeserializePreset(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return Result.Fail("输入的预设JSON内容为空或无效。");
        }

        try
        {
            var preset = JsonSerializer.Deserialize<SillyTavernPreset>(jsonContent);

            if (preset == null)
            {
                return Result.Fail("预设JSON解析结果为空，这可能是因为内容为 'null' 或结构不兼容。");
            }
            
            // 基本验证：确保核心部分存在
            if (preset.Prompts == null || preset.PromptOrder == null)
            {
                return Result.Fail("预设JSON结构不完整，缺少 'prompts' 或 'prompt_order' 关键字段。");
            }

            return Result.Ok(preset);
        }
        catch (JsonException ex)
        {
            // 捕获JSON解析异常，并返回一个友好的错误信息
            return Result.Fail("预设JSON格式错误，无法解析。",ex);
        }
    }

    /// <summary>
    /// 尝试将一个 JSON 字符串反序列化为 SillyTavernWorldInfo 对象。
    /// </summary>
    /// <param name="jsonContent">包含世界书数据的 JSON 字符串。</param>
    /// <returns>
    /// 如果成功，返回一个包含 <see cref="SillyTavernWorldInfo"/> 实例的 Result.Ok；
    /// 如果失败，返回一个包含中文错误信息的 Result.Fail。
    /// </returns>
    public static Result<SillyTavernWorldInfo> DeserializeWorldInfo(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return Result.Fail("输入的世界书JSON内容为空或无效。");
        }

        try
        {
            var worldInfo = JsonSerializer.Deserialize<SillyTavernWorldInfo>(jsonContent);

            if (worldInfo == null)
            {
                return Result.Fail("世界书JSON解析结果为空，这可能是因为内容为 'null' 或结构不兼容。");
            }
            
            // 基本验证
            if (worldInfo.Entries == null)
            {
                return Result.Fail("世界书JSON结构不完整，缺少 'entries' 关键字段。");
            }

            return Result.Ok(worldInfo);
        }
        catch (JsonException ex)
        {
            return Result.Fail("世界书JSON格式错误，无法解析。",ex);
        }
    }
}