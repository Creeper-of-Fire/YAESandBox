using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using UUIDNext;
using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.Config.Stored;

namespace YAESandBox.Workflow.API.Service;

/// <summary>
/// 负责在应用启动时，发现、聚合并缓存所有模块提供的内置配置。
/// </summary>
public static class InnerConfigProviderResolver
{
    /// <summary>
    /// 主命名空间，用于根据 模块ID + RefId + Version 生成确定性ID
    /// </summary>
    private static Guid ProviderNamespace { get; } = new("b48821d4-f76d-4bbb-b1ce-41d8693d62a7");

    /// <summary>
    /// 备用命名空间，用于在主ID冲突时，根据配置内容的哈希生成降级ID
    /// </summary>
    private static Guid FallbackNamespace { get; } = new("3e9c5a4f-5c8c-4e8d-8a8b-2c6f7b1d9a0e");

    private static IAppLogger Logger { get; } = AppLogging.CreateLogger(nameof(InnerConfigProviderResolver));

    /// <summary>
    /// 获取所有已注册的内部工作流配置。
    /// </summary>
    public static IReadOnlyDictionary<string, StoredConfig<WorkflowConfig>> WorkflowInnerConfigs => InnerWorkflowConfigsCache;

    /// <summary>
    /// 获取所有已注册的内部枢机配置。
    /// </summary>
    public static IReadOnlyDictionary<string, StoredConfig<TuumConfig>> TuumInnerConfigs => InnerTuumConfigsCache;

    /// <summary>
    /// 获取所有已注册的内部符文配置。
    /// </summary>
    public static IReadOnlyDictionary<string, StoredConfig<AbstractRuneConfig>> RuneInnerConfigs => InnerRuneConfigsCache;

    /// <summary>
    /// 缓存所有内部工作流配置。Key: UUID V5, Value: StoredConfig
    /// </summary>
    private static IReadOnlyDictionary<string, StoredConfig<WorkflowConfig>> InnerWorkflowConfigsCache { get; set; } =
        new Dictionary<string, StoredConfig<WorkflowConfig>>();

    /// <summary>
    /// 缓存所有内部枢机配置。Key: UUID V5, Value: StoredConfig
    /// </summary>
    private static IReadOnlyDictionary<string, StoredConfig<TuumConfig>> InnerTuumConfigsCache { get; set; } =
        new Dictionary<string, StoredConfig<TuumConfig>>();

    /// <summary>
    /// 缓存所有内部符文配置。Key: UUID V5, Value: StoredConfig
    /// </summary>
    private static IReadOnlyDictionary<string, StoredConfig<AbstractRuneConfig>> InnerRuneConfigsCache { get; set; } =
        new Dictionary<string, StoredConfig<AbstractRuneConfig>>();

    /// <summary>
    /// 初始化解析器，遍历所有提供者模块，收集并缓存它们的内部配置。
    /// </summary>
    public static void Initialize(IEnumerable<IProgramModuleInnerConfigProvider> innerConfigProviders)
    {
        var finalWorkflows = new Dictionary<string, StoredConfig<WorkflowConfig>>();
        var finalTuums = new Dictionary<string, StoredConfig<TuumConfig>>();
        var finalRunes = new Dictionary<string, StoredConfig<AbstractRuneConfig>>();

        foreach (var provider in innerConfigProviders)
        {
            string? moduleId = provider.GetType().FullName;
            if (string.IsNullOrEmpty(moduleId))
            {
                moduleId = provider.GetType().FullName ?? $"anonymous-provider:{provider.GetType().Name}:{Guid.NewGuid()}";
                Logger.Warn(
                    "发现一个无法确定稳定FullName的 " + nameof(IProgramModuleInnerConfigProvider) +
                    " (类型: {ProviderType})。已为其生成临时的会话唯一ID: '{ModuleId}'。",
                    provider.GetType().Name, moduleId);
            }

            // 处理单个提供者的配置，解决其内部的冲突
            var processedWorkflows = ProcessProviderItems(provider.GetWorkflowInnerConfigs(), moduleId);
            var processedTuums = ProcessProviderItems(provider.GetTuumInnerConfigs(), moduleId);
            var processedRunes = ProcessProviderItems(provider.GetRuneInnerConfigs(), moduleId);

            // 直接合并，因为从概率意义上，不同 provider 的结果集不可能有 key 冲突。
            // 但为防止极低概率的哈希碰撞或比特翻转（显然比特翻转的可能性会更高），我们使用 TryAdd。
            foreach ((string id, var config) in processedWorkflows)
                finalWorkflows.TryAdd(id, config);
            foreach ((string id, var config) in processedTuums)
                finalTuums.TryAdd(id, config);
            foreach ((string id, var config) in processedRunes)
                finalRunes.TryAdd(id, config);
        }

        InnerWorkflowConfigsCache = new ReadOnlyDictionary<string, StoredConfig<WorkflowConfig>>(finalWorkflows);
        InnerTuumConfigsCache = new ReadOnlyDictionary<string, StoredConfig<TuumConfig>>(finalTuums);
        InnerRuneConfigsCache = new ReadOnlyDictionary<string, StoredConfig<AbstractRuneConfig>>(finalRunes);
    }

    /// <summary>
    /// 处理来自【单个】提供者的配置列表。
    /// 它负责解决该提供者内部的 ID 冲突，并返回一个全新的、干净的字典。
    /// </summary>
    [Pure]
    private static IReadOnlyDictionary<string, StoredConfig<TConfig>> ProcessProviderItems<TConfig>(
        IEnumerable<StoredConfig<TConfig>> providerConfigs, string moduleId) where TConfig : IConfigStored
    {
        var resultDictionary = new Dictionary<string, StoredConfig<TConfig>>();

        // 预处理所有配置，强制 IsReadOnly = true。
        var sanitizedConfigs = providerConfigs.Select(config => EnforceReadOnlyPolicy(config, moduleId));

        // 按主ID对已净化的配置进行分组，这能立即暴露冲突。
        var groups = sanitizedConfigs.GroupBy(config => GeneratePrimaryId(moduleId, config));

        foreach (var group in groups)
        {
            string primaryId = group.Key;
            var itemsInGroup = group.ToList();

            if (itemsInGroup.Count == 0)
            {
                Logger.Critical("GroupBy 产生了空的分组，这违反了 LINQ 的基本约定。系统可能存在严重问题。");
                continue;
            }

            var firstItem = itemsInGroup[0];

            if (itemsInGroup.Count == 1)
            {
                // 完美情况：没有冲突，直接添加
                resultDictionary.Add(primaryId, firstItem);
                continue;
            }

            // 【冲突发生】
            // 这通常意味着模块提供了多个具有相同 RefId:Version 的配置，或者多个内容完全相同的无引用配置。
            string conflictSource = firstItem.StoreRef is not null
                ? $"RefId:Version '{firstItem.StoreRef.RefId}:{firstItem.StoreRef.Version}'"
                : "内容哈希（因为StoreRef为空）";

            Logger.Error(
                "模块内部配置冲突！模块 '{ModuleId}' 为 {ConflictSource} 提供了 {Count} 个重复的配置项。将为每个重复项尝试生成备用ID。",
                moduleId, conflictSource, itemsInGroup.Count);

            foreach (var conflictingItem in itemsInGroup)
            {
                string fallbackId = GenerateFallbackId(moduleId, conflictingItem);

                if (resultDictionary.TryAdd(fallbackId, conflictingItem))
                {
                    Logger.Warn("降级成功。冲突的配置 '{ConfigName}' (来自模块 '{ModuleId}') 已使用备用ID '{FallbackId}' 加载。",
                        conflictingItem.Name, moduleId, fallbackId);
                }
                else
                {
                    Logger.Error("降级失败，备用ID '{FallbackId}' 也已存在，说明模块 '{ModuleId}' 提供了内容完全重复的配置。将丢弃后一个 '{ConfigName}'。",
                        fallbackId, moduleId, conflictingItem.Name);
                }
            }
        }

        return new ReadOnlyDictionary<string, StoredConfig<TConfig>>(resultDictionary);
    }

    /// <summary>
    /// 强制执行“内置配置必须为只读”的策略。
    /// </summary>
    private static StoredConfig<TConfig> EnforceReadOnlyPolicy<TConfig>(StoredConfig<TConfig> config, string moduleId)
        where TConfig : IConfigStored
    {
        if (config.IsReadOnly)
        {
            return config;
        }

        Logger.Warn(
            "模块 '{ModuleId}' 提供了一个非只读的内置配置 '{ConfigName}'。所有内置配置必须是只读的。将强制设置为只读。",
            moduleId, config.Name);

        // 使用 'with' 表达式创建一个新的、不可变的副本，并修正 IsReadOnly 属性。
        return config with { IsReadOnly = true };
    }

    /// <summary>
    /// 生成一个确定性的主存储ID (UUIDv5)。
    /// 如果 StoreRef 存在，ID 基于 模块ID -> (RefId + Version)。
    /// 如果 StoreRef 为空，ID 基于内容的JSON序列化字符串，确保内容唯一性。
    /// </summary>
    private static string GeneratePrimaryId<TConfig>(string moduleId, StoredConfig<TConfig> config) where TConfig : IConfigStored
    {
        // 没有引用，使用内容哈希生成ID
        if (config.StoreRef is null) 
            return GenerateFallbackId(moduleId, config);
        
        // 有引用，使用引用信息生成ID
        string localIdentifier = $"{config.StoreRef.RefId}:{config.StoreRef.Version}";
        var moduleUuid = Uuid.NewNameBased(ProviderNamespace, moduleId);
        return Uuid.NewNameBased(moduleUuid, localIdentifier).ToString();
    }

    /// <summary>
    /// 尝试生成一个备用ID。
    /// </summary>
    private static string GenerateFallbackId<TConfig>(string moduleId, StoredConfig<TConfig> config) where TConfig : IConfigStored
    {
        try
        {
            string contentJson = YaeSandBoxJsonHelper.Serialize(config.Content);
            // 使用备用命名空间来避免与模块ID+引用ID的组合发生潜在冲突
            return Uuid.NewNameBased(FallbackNamespace, contentJson).ToString();
        }
        catch (Exception ex)
        {
            // 这是一个严重问题，因为我们无法为这个配置生成一个稳定的ID
            string emergencyId = Uuid.NewSequential().ToString();
            Logger.Error(ex,
                "在为模块 '{ModuleId}' 中名为 '{ConfigName}' 的无引用配置生成基于内容的ID时发生序列化异常。将为其分配一个临时的随机ID '{EmergencyId}'，但这会在应用重启后改变！",
                moduleId, config.Name, emergencyId);
            return emergencyId;
        }
    }
}