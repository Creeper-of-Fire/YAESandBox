using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FluentResults;
using YAESandBox.Workflow.AIService.AiConfigSchema;
using YAESandBox.Workflow.AIService.ConfigManagement;

namespace YAESandBox.Workflow.AIService.AiConfig;

/// <summary>
/// 代表一个 AI 配置集，它包含了一组特定类型的 AI 配置。
/// </summary>
public class AiConfigurationSet
{
    /// <summary>
    /// 创建一个默认的 AI 配置集。
    /// </summary>
    /// <returns></returns>
    public static AiConfigurationSet MakeDefault() => new() { ConfigSetName = "Default" };

    /// <summary>
    /// 用户为配置集指定的名称，用于在 UI 上显示和识别。
    /// </summary>
    [Display(
        Name = "AiConfigurationSet_ConfigSetName_Label",
        Description = "AiConfigurationSet_ConfigSetName_Description",
        ResourceType = typeof(AiProcessorConfigResources))]
    [Required]
    public string ConfigSetName { get; init; } = string.Empty;

    /// <summary>
    /// 包含在此配置集中的具体 AI 配置。
    /// Key 是 AI 配置的模块类型 (ModuleType, 例如 "DoubaoAiProcessorConfig")。
    /// Value 是该模块类型的具体配置数据对象 (不包含 ConfigName 和 ModuleType 字段本身)。
    /// </summary>
    [JsonConverter(typeof(AiConfigurationSetDictionaryConverter))]
    [Required]
    public Dictionary<string, AbstractAiProcessorConfig> Configurations { get; init; } = new();

    /// <inheritdoc cref="ConfigSchemasHelper.GetAvailableAiConfigConcreteTypes"/>
    public static IEnumerable<Type> GetAvailableAiConfigTypes() => ConfigSchemasHelper.GetAvailableAiConfigConcreteTypes();

    /// <inheritdoc cref="ConfigSchemasHelper.GetTypeByName"/>
    public static Type? GetAiConfigTypeByName(string typeName) => ConfigSchemasHelper.GetTypeByName(typeName);

    /// <summary>
    /// 尝试获取指定模块类型的AI配置
    /// </summary>
    /// <param name="moduleType"></param>
    /// <returns></returns>
    public Result<AbstractAiProcessorConfig> FindAiConfig(string moduleType)
    {
        this.Configurations.TryGetValue(moduleType, out var config);
        if (config == null)
            return AIConfigError.Error($"{this.ConfigSetName}中未定义 AI 配置类型: {moduleType}");
        return Result.Ok(config);
    }

    /// <summary>
    /// 获取所有已存在的Config的类型名
    /// </summary>
    /// <returns></returns>
    public List<string> GetAllDefinedTypes() => this.Configurations.Keys.ToList();
}