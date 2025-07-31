using System.Collections.ObjectModel;
using System.Reflection;
using YAESandBox.Workflow.Config;

namespace YAESandBox.Workflow.Utility;

/// <summary>
/// 辅助类，用于根据模块类型名称查找具体的 <see cref="AbstractModuleConfig"/> 实现类型，
/// 并在初始化时缓存所有可用的实现。
/// </summary>
internal static class ModuleConfigTypeResolver
{
    // 缓存所有已解析的类型。键为模块类型名称（不区分大小写），值为对应的 Role。
    // 使用 IReadOnlyDictionary 确保初始化后的不可变性。
    private static readonly IReadOnlyDictionary<string, Type> ResolvedTypesCache;

    private static readonly Type InterfaceType = typeof(AbstractModuleConfig);

    // 假设所有 AbstractModuleConfig 的实现都在 AbstractModuleConfig 接口所在的程序集中。
    // 如果实现可能分布在多个程序集中，需要调整 TargetAssemblies 的获取逻辑。
    private static readonly Assembly TargetAssembly = InterfaceType.Assembly; // TODO: 确认扫描范围是否足够

    /// <summary>
    /// 静态构造函数，在类首次被访问时执行。
    /// 负责扫描程序集并构建 ResolvedTypesCache。
    /// </summary>
    static ModuleConfigTypeResolver()
    {
        var temporaryDictionary = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        // TODO: 如果需要扫描多个程序集，在此处调整 GetTypes() 的来源
        var allTypesInAssembly = TargetAssembly.GetTypes();

        foreach (var type in allTypesInAssembly)
        {
            // 确保类型是非抽象的类，并且实现了 AbstractModuleConfig
            if (type is not { IsClass: true, IsAbstract: false } || !InterfaceType.IsAssignableFrom(type))
                continue;
            // 约定：ModuleType 的值等于类名。
            // 这个值应该与 AbstractModuleConfig 构造函数中传入的 nameof(ConcreteType) 一致。
            string moduleTypeName = type.Name;

            if (string.IsNullOrWhiteSpace(moduleTypeName))
            {
                // 理论上 type.Name 不会是空或空白，但作为防御性检查。
                // 可以考虑记录一个警告或内部错误。
                continue;
            }

            // 处理 ModuleType 名称冲突 (忽略大小写)
            if (temporaryDictionary.TryGetValue(moduleTypeName, out var existingType))
            {
                // 如果已存在一个同名（忽略大小写）的模块类型，则抛出异常。
                throw new InvalidOperationException(
                    $"模块类型名称冲突：类型 '{type.FullName}' 和 '{existingType.FullName}' " +
                    $"都希望注册为模块类型 '{moduleTypeName}' (忽略大小写)。模块类型名称必须唯一。");
            }

            temporaryDictionary.Add(moduleTypeName, type);
        }

        ResolvedTypesCache = new ReadOnlyDictionary<string, Type>(temporaryDictionary);
    }

    /// <summary>
    /// 根据从JSON中读取的模块类型名称查找具体的 <see cref="AbstractModuleConfig"/> 实现类型。
    /// </summary>
    /// <param name="moduleTypeNameFromInput">从JSON中读取的模块类型名称。</param>
    /// <returns>找到的具体实现类型；如果未找到，则返回 null。</returns>
    public static Type? FindModuleConfigType(string moduleTypeNameFromInput)
    {
        if (string.IsNullOrWhiteSpace(moduleTypeNameFromInput))
            return null;

        ResolvedTypesCache.TryGetValue(moduleTypeNameFromInput, out var foundType);
        return foundType;
    }

    /// <summary>
    /// 获取所有已注册的 <see cref="AbstractModuleConfig"/> 实现类型。
    /// </summary>
    /// <returns>一个包含所有模块配置实现类型的集合。</returns>
    public static IEnumerable<Type> GetAllModuleConfigTypes()
    {
        return ResolvedTypesCache.Values;
    }

    /// <summary>
    /// 获取所有已注册的模块类型名称及其对应的 <see cref="Type"/>。
    /// </summary>
    /// <returns>一个只读字典，键是模块类型名称 (通常是类名，忽略大小写)，值是类型。</returns>
    public static IReadOnlyDictionary<string, Type> GetModuleTypeMappings()
    {
        return ResolvedTypesCache;
    }
}