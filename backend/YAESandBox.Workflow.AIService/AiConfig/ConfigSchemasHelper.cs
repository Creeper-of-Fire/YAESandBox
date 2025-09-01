// ConfigSchemasHelper.cs

using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Extensions.Logging;
using YAESandBox.Depend;

namespace YAESandBox.Workflow.AIService.AiConfig;

/// <summary>
/// 帮助类，用于根据C#类型定义动态生成前端表单所需的Schema结构，
/// 以及提供对特定配置类型的发现和访问。
/// 它通过反射读取类型的属性及其关联的DataAnnotations特性。
/// </summary>
internal static class ConfigSchemasHelper // 改为静态类，因为所有成员都是静态的
{
    private static ILogger Logger { get; } = AppLogging.CreateLogger(nameof(ConfigSchemasHelper));

    // 假设 AbstractAiProcessorConfig 是定义 AI 处理器配置的基类
    private static Type AbstractAiProcessorConfigType { get; } = typeof(AbstractAiProcessorConfig);

    // 缓存所有继承自 AbstractAiProcessorConfig 的具体配置类型。
    // 键为类型名称（不区分大小写），值为对应的 Role。
    private static IReadOnlyDictionary<string, Type> AvailableAiConfigTypesCache { get; }

    /// <summary>
    /// 静态构造函数，在类首次被访问时执行。
    /// 负责扫描程序集并构建 AvailableAiConfigTypesCache。
    /// </summary>
    static ConfigSchemasHelper()
    {
        var temporaryDictionary = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        // 假设所有配置类都在 AbstractAiProcessorConfig 所在的程序集中。
        // 如果分布在多个程序集中，需要调整 Assembly.GetAssembly() 或提供程序集列表。
        var targetAssembly = Assembly.GetAssembly(AbstractAiProcessorConfigType);

        if (targetAssembly == null)
        {
            Logger.LogError("[ERROR] 无法获取类型 '{FullName}' 所在的程序集。AI 配置类型将无法被发现。", AbstractAiProcessorConfigType.FullName);
            AvailableAiConfigTypesCache = new ReadOnlyDictionary<string, Type>(temporaryDictionary); // 初始化为空字典
            return;
        }

        var allTypesInAssembly = targetAssembly.GetTypes();

        foreach (var type in allTypesInAssembly)
        {
            // 确保类型是类、非抽象，并且继承自 AbstractAiProcessorConfigType。
            // 移除了 IsPublic 检查，允许 internal 类型。
            if (type is not { IsClass: true, IsAbstract: false } || !AbstractAiProcessorConfigType.IsAssignableFrom(type))
                continue;
            string typeName = type.Name; // 使用类名作为键

            if (string.IsNullOrWhiteSpace(typeName))
            {
                continue; // 理论上 type.Name 不会是空或空白
            }

            // 处理类型名称冲突 (忽略大小写)
            if (temporaryDictionary.TryGetValue(typeName, out var existingType))
            {
                Logger.LogError(
                    "[ERROR] AI 配置类型名称冲突：类型 '{TypeFullName}' 和 '{ExistingTypeFullName}' 都具有相同的类名 '{TypeName}' (忽略大小写)。类名必须在该上下文中唯一。",
                    type.FullName, existingType.FullName, typeName);
                // 可以选择抛出异常或跳过冲突的类型，这里选择记录错误并跳过后来者（或先来者，取决于字典行为）
                // 如果严格要求唯一性，则应抛出 InvalidOperationException
                // throw new InvalidOperationException(errorMessage);
                continue;
            }

            temporaryDictionary.Add(typeName, type);
        }

        AvailableAiConfigTypesCache = new ReadOnlyDictionary<string, Type>(temporaryDictionary);

        Logger.LogInformation("[INFO] ConfigSchemasHelper: 已发现 {Count} 个 AI 配置类型。", AvailableAiConfigTypesCache.Count);
        foreach (var (name, type) in AvailableAiConfigTypesCache)
        {
            Logger.LogDebug("[DEBUG] ConfigSchemasHelper: 发现 AI 配置: {Name} -> {TypeFullName}", name, type.FullName);
        }
    }

    /// <summary>
    /// 获取所有继承自 <see cref="AbstractAiProcessorConfig"/> 的具体配置类型。
    /// </summary>
    /// <returns>一个包含所有可用AI配置类型的集合。</returns>
    internal static IEnumerable<Type> GetAvailableAiConfigConcreteTypes()
    {
        return AvailableAiConfigTypesCache.Values;
    }

    /// <summary>
    /// 根据类型的编程名称（类名）安全地获取 AI 配置类型。
    /// 比较时忽略大小写。
    /// </summary>
    /// <param name="typeName">类型的名称 (例如 "DoubaoAiProcessorConfig")。</param>
    /// <returns>如果找到则返回类型，否则throw。</returns>
    internal static Type? GetAiConfigTypeByName(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;
        AvailableAiConfigTypesCache.TryGetValue(typeName, out var foundType);
        return foundType;
    }
}