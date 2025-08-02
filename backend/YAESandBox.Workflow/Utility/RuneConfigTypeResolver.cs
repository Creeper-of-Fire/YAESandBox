using System.Collections.ObjectModel;
using System.Reflection;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Workflow.API;
using YAESandBox.Workflow.Config;

namespace YAESandBox.Workflow.Utility;

/// <summary>
/// 辅助类，用于根据符文类型名称查找具体的 <see cref="AbstractRuneConfig"/> 实现类型，
/// 并在初始化时缓存所有可用的实现。
/// </summary>
internal static class RuneConfigTypeResolver
{
    // 缓存所有已解析的类型。键为符文类型名称（不区分大小写），值为对应的 Role。
    // 使用 IReadOnlyDictionary 确保初始化后的不可变性。
    private static IReadOnlyDictionary<string, Type> ResolvedTypesCache { get; set; } = new Dictionary<string, Type>();

    private static readonly Type InterfaceType = typeof(AbstractRuneConfig);


    /// <summary>
    /// 初始化解析器，通过查询所有模块来发现并注册符文类型。
    /// 这个方法应该在应用启动时，在所有模块被发现后调用一次。
    /// </summary>
    /// <param name="runeProviders">应用程序中所有已发现的模块实例。</param>
    public static void Initialize(IEnumerable<IProgramModuleRuneProvider> runeProviders)
    {
        var temporaryDictionary = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in runeProviders)
        {
            // 2. 从每个提供者模块获取它声明的符文类型
            var runeTypesFromModule = module.RuneConfigTypes;

            foreach (var type in runeTypesFromModule)
            {
                // 3. 执行与之前相同的验证和注册逻辑
                if (type is not { IsClass: true, IsAbstract: false } || !InterfaceType.IsAssignableFrom(type))
                {
                    // 可以在这里记录一个警告，因为插件声明了一个无效的符文类型
                    Console.WriteLine($"[警告] 模块 '{module.GetType().Name}' 提供的类型 '{type.Name}' 不是一个有效的符文配置类，将被忽略。");
                    continue;
                }

                // 约定：RuneType 的值等于类名。
                string runeTypeName = type.Name;

                if (temporaryDictionary.TryGetValue(runeTypeName, out var existingType))
                {
                    throw new InvalidOperationException(
                        $"符文类型名称冲突：类型 '{type.FullName}' 和 '{existingType.FullName}' " +
                        $"都希望注册为符文类型 '{runeTypeName}' (忽略大小写)。符文类型名称必须唯一。");
                }

                temporaryDictionary.Add(runeTypeName, type);
                Console.WriteLine($"[RuneResolver] 已从模块 '{module.GetType().Name}' 注册符文: {runeTypeName}");
            }
        }

        ResolvedTypesCache = new ReadOnlyDictionary<string, Type>(temporaryDictionary);
    }

    /// <summary>
    /// 根据从JSON中读取的符文类型名称查找具体的 <see cref="AbstractRuneConfig"/> 实现类型。
    /// </summary>
    /// <param name="runeTypeNameFromInput">从JSON中读取的符文类型名称。</param>
    /// <returns>找到的具体实现类型；如果未找到，则返回 null。</returns>
    public static Type? FindRuneConfigType(string runeTypeNameFromInput)
    {
        if (string.IsNullOrWhiteSpace(runeTypeNameFromInput))
            return null;

        ResolvedTypesCache.TryGetValue(runeTypeNameFromInput, out var foundType);
        return foundType;
    }

    /// <summary>
    /// 获取所有已注册的 <see cref="AbstractRuneConfig"/> 实现类型。
    /// </summary>
    /// <returns>一个包含所有符文配置实现类型的集合。</returns>
    public static IEnumerable<Type> GetAllRuneConfigTypes()
    {
        return ResolvedTypesCache.Values;
    }

    /// <summary>
    /// 获取所有已注册的符文类型名称及其对应的 <see cref="Type"/>。
    /// </summary>
    /// <returns>一个只读字典，键是符文类型名称 (通常是类名，忽略大小写)，值是类型。</returns>
    public static IReadOnlyDictionary<string, Type> GetRuneTypeMappings()
    {
        return ResolvedTypesCache;
    }
}