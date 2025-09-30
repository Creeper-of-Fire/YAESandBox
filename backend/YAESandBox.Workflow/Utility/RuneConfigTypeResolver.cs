using System.Collections.ObjectModel;
using YAESandBox.Depend.Logger;
using YAESandBox.Workflow.API;
using YAESandBox.Workflow.Rune;

namespace YAESandBox.Workflow.Utility;

/// <summary>
/// 辅助类，用于根据符文类型名称查找具体的 <see cref="AbstractRuneConfig"/> 实现类型，
/// 并在初始化时缓存所有可用的实现。
/// </summary>
internal static class RuneConfigTypeResolver
{
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger(nameof(RuneConfigTypeResolver));

    /// <summary>
    /// 缓存所有已解析的类型。键为符文类型名称（不区分大小写），值为对应的 Role。
    /// 使用 IReadOnlyDictionary 确保初始化后的不可变性。
    /// </summary>
    private static IReadOnlyDictionary<string, Type> ResolvedTypesCache { get; set; } = new Dictionary<string, Type>();

    /// <summary>
    /// 缓存：Type -> 提供了该类型的模块
    /// </summary>
    private static IReadOnlyDictionary<Type, IProgramModuleRuneProvider> TypeToProviderModuleCache { get; set; } =
        new Dictionary<Type, IProgramModuleRuneProvider>();


    private static readonly Type InterfaceType = typeof(AbstractRuneConfig);


    /// <summary>
    /// 初始化解析器，通过查询所有模块来发现并注册符文类型。
    /// 这个方法应该在应用启动时，在所有模块被发现后调用一次。
    /// </summary>
    /// <param name="runeProviders">应用程序中所有已发现的模块实例。</param>
    public static void Initialize(IEnumerable<IProgramModuleRuneProvider> runeProviders)
    {
        var temporaryDictionary = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        var typeToProviderDict = new Dictionary<Type, IProgramModuleRuneProvider>();

        foreach (var provider in runeProviders)
        {
            // 2. 从每个提供者模块获取它声明的符文类型
            var runeTypesFromModule = provider.RuneConfigTypes;

            foreach (var type in runeTypesFromModule)
            {
                string providerTypeName = provider.GetType().Name;
                // 约定：RuneType 的值等于类名。
                string runeTypeName = type.Name;

                // 3. 执行与之前相同的验证和注册逻辑
                if (type is not { IsClass: true, IsAbstract: false } || !InterfaceType.IsAssignableFrom(type))
                {
                    // 可以在这里记录一个警告，因为插件声明了一个无效的符文类型
                    Logger.Warn("[警告] 模块 '{ProviderTypeName}' 提供的类型 '{RuneTypeName}' 不是一个有效的符文配置类，将被忽略。", providerTypeName, runeTypeName);
                    continue;
                }

                if (temporaryDictionary.TryGetValue(runeTypeName, out var existingType))
                {
                    throw new InvalidOperationException(
                        $"符文类型名称冲突：类型 '{type.FullName}' 和 '{existingType.FullName}' " +
                        $"都希望注册为符文类型 '{runeTypeName}' (忽略大小写)。符文类型名称必须唯一。");
                }

                temporaryDictionary.Add(runeTypeName, type);
                typeToProviderDict.Add(type, provider);
                Logger.Info("[RuneResolver] 已从模块 '{ProviderTypeName}' 注册符文: {RuneTypeName}", providerTypeName, runeTypeName);
            }
        }

        ResolvedTypesCache = new ReadOnlyDictionary<string, Type>(temporaryDictionary);
        TypeToProviderModuleCache = new ReadOnlyDictionary<Type, IProgramModuleRuneProvider>(typeToProviderDict);
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
    /// 根据符文配置类型，查找其所属的插件模块。
    /// </summary>
    /// <param name="runeConfigType">符文配置的 Type 对象。</param>
    /// <returns>提供该符文的模块实例；如果找不到（例如，是内置符文），则返回 null。</returns>
    internal static IProgramModuleRuneProvider? GetProviderModuleForType(Type runeConfigType)
    {
        TypeToProviderModuleCache.TryGetValue(runeConfigType, out var provider);
        return provider;
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