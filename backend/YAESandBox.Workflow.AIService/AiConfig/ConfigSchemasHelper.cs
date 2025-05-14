// ConfigSchemasHelper.cs

using System.Reflection;
using YAESandBox.Depend;

namespace YAESandBox.Workflow.AIService.AiConfig;

/// <summary>
/// 帮助类，用于根据C#类型定义动态生成前端表单所需的Schema结构。
/// 它通过反射读取类型的属性及其关联的DataAnnotations特性。
/// </summary>
internal class ConfigSchemasHelper
{
    private static List<Type>? _availableConfigTypesCache;
    private static readonly Lock LockAvailableConfigTypes = new();

    /// <summary>
    /// 获取所有继承自 AbstractAiProcessorConfig 的具体配置类型。
    /// 使用缓存以提高性能。
    /// </summary>
    /// <returns>类型列表。</returns>
    internal static IEnumerable<Type> GetAvailableAiConfigConcreteTypes()
    {
        // 先尝试快速返回（无锁）
        var cached = _availableConfigTypesCache;
        if (cached != null)
            return cached;
        lock (LockAvailableConfigTypes)
        {
            // 双重检查锁定模式
            // 再次检查缓存是否已创建（锁内二次检查）
            cached = _availableConfigTypesCache;
            if (cached != null)
                return cached;
            var abstractConfigType = typeof(AbstractAiProcessorConfig);
            // 假设所有配置类都在 AbstractAiProcessorConfig 所在的程序集中。
            // 如果分布在多个程序集中，需要调整 Assembly.GetAssembly() 或提供程序集列表。
            _availableConfigTypesCache = Assembly.GetAssembly(abstractConfigType)!
                .GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && abstractConfigType.IsAssignableFrom(t))
                .ToList();

            int count = _availableConfigTypesCache.Count;
            Log.Info($"已发现 {count} 个 AI 配置类型。");
        }

        return _availableConfigTypesCache;
    }

    /// <summary>
    /// 根据类型的编程名称安全地获取 AI 配置类型。
    /// </summary>
    /// <param name="typeName">类型的名称 (例如 "DoubaoAiProcessorConfig")。</param>
    /// <returns>如果找到则返回类型，否则返回 null。</returns>
    internal static Type? GetTypeByName(string typeName) // 使用 new 关键字隐藏基类或其他同名方法（如果存在）
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        return GetAvailableAiConfigConcreteTypes()
            .FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
    }
}