using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Secret.Mark;

namespace YAESandBox.Workflow.AIService.AiConfig;

/// <summary>
/// 代表一个 AI 配置集，它包含了一组特定类型的 AI 配置。
/// </summary>
public class AiConfigurationSet : IProtectedData
{
    /// <summary>
    /// 默认的 AI 配置集名称。
    /// </summary>
    public const string DefaultConfigSetName = "Default";

    /// <summary>
    /// 创建一个默认的 AI 配置集。
    /// </summary>
    /// <returns></returns>
    public static AiConfigurationSet MakeDefault() => new() { ConfigSetName = DefaultConfigSetName };

    /// <summary>
    /// 创建一个 AI 配置集字典，并且放入 一个默认的配置集。
    /// </summary>
    /// <returns></returns>
    public static Dictionary<string, AiConfigurationSet> MakeDefaultDictionary() => new() { { Guid.NewGuid().ToString(), MakeDefault() } };

    /// <summary>
    /// 用户为配置集指定的名称，用于在 UI 上显示和识别。
    /// </summary>
    [Display(
        Name = "配置集名称",
        Description = "为此组AI配置指定一个易于识别的名称。")]
    [Required]
    public string ConfigSetName { get; init; } = string.Empty;

    /// <summary>
    /// 包含在此配置集中的具体 AI 配置。
    /// Key 是 AI 配置的模块类型 (ModuleType, 例如 "DoubaoAiProcessorConfig")。
    /// Value 是该模块类型的具体配置数据对象 (不包含 ConfigName 和 ModuleType 字段本身)。
    /// </summary>
    [Required]
    public Dictionary<string, AbstractAiProcessorConfig> Configurations { get; init; } = new();

    /// <inheritdoc cref="ConfigSchemasHelper.GetAvailableAiConfigConcreteTypes"/>
    public static IEnumerable<Type> GetAvailableAiConfigTypes() => ConfigSchemasHelper.GetAvailableAiConfigConcreteTypes();

    /// <inheritdoc cref="ConfigSchemasHelper.GetAiConfigTypeByName"/>
    public static Type? GetAiConfigTypeByName(string typeName) => ConfigSchemasHelper.GetAiConfigTypeByName(typeName);

    /// <summary>
    /// 尝试获取指定模块类型的AI配置
    /// </summary>
    /// <param name="aiModelType"></param>
    /// <returns></returns>
    public Result<AbstractAiProcessorConfig> FindAiConfig(string aiModelType)
    {
        this.Configurations.TryGetValue(aiModelType, out var config);
        if (config == null)
            return NormalError.NotFound($"{this.ConfigSetName}中未定义 AI 配置类型: {aiModelType}");
        return Result.Ok(config);
    }

    /// <summary>
    /// 获取所有已存在的Config的类型名
    /// </summary>
    /// <returns></returns>
    public List<string> GetAllDefinedTypes() => this.Configurations.Keys.ToList();
}