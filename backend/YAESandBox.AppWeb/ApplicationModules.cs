using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using YAESandBox.AppWeb.InnerModule;
using YAESandBox.AppWeb.Services;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;
using YAESandBox.Depend.Logger;
using YAESandBox.PlayerServices.Save;
using YAESandBox.Workflow.AIService.API;
using YAESandBox.Workflow.API;
using YAESandBox.Workflow.Test.API;

namespace YAESandBox.AppWeb;

internal static class ApplicationModules
{
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger(nameof(ApplicationModules));

    // 一个静态字典来持有我们创建的所有插件加载上下文
    private static Dictionary<string, PluginAssemblyLoadContext> PluginLoadContexts { get; } = new();

    private static bool _isResolvingEventHooked;

    // 使用 AsyncLocal 来跟踪当前线程正在解析的程序集，以防止无限递归。
    private static AsyncLocal<ConcurrentDictionary<string, bool>> ResolvingAssemblies { get; } = new();

    /// <summary>
    /// 发现并加载所有模块，包括内置模块和来自插件目录的动态模块。
    /// </summary>
    /// <param name="pluginDiscoveryService"></param>
    /// <returns>一个包含所有模块实例和加载的插件程序集的元组。</returns>
    public static (IReadOnlyList<IProgramModule> Modules, IReadOnlyList<Assembly> PluginAssemblies)
        DiscoverAndLoadAllModules(IPluginDiscoveryService pluginDiscoveryService)
    {
        // 2. 【新增】在第一次加载时，挂载我们的“救援”事件处理器
        if (!_isResolvingEventHooked)
        {
            AssemblyLoadContext.Default.Resolving += ResolvePluginDependency;
            _isResolvingEventHooked = true;
            Logger.Info("[插件加载器] 已挂载默认上下文的程序集解析事件。");
        }

        // 1. 定义内置的核心模块列表
        var coreModules = CoreModules;

        var loadedPluginAssemblies = new List<Assembly>();
        var pluginModules = new List<IProgramModule>();

        // 2. 从发现服务获取所有插件
        var discoveredPlugins = pluginDiscoveryService.DiscoverPlugins();

        // 1. 遍历每个插件目录
        foreach (var plugin in discoveredPlugins)
        {
            // 2. 【核心】在目录中查找所有作为“入口点”的DLL。
            //    判断标准：该DLL拥有一个同名的 .deps.json 文件。
            var entryPointDlls = plugin.DllPaths
                .Where(dllPath => File.Exists(Path.ChangeExtension(dllPath, ".deps.json")))
                .ToList();

            if (entryPointDlls.Count == 0)
            {
                if (plugin.DllPaths.Count > 0)
                    Logger.Info("[插件加载器] 警告: 插件 '{PluginName}' 目录中存在DLL，但未找到任何带有 .deps.json 的入口点程序集。", plugin.Name);
                continue;
            }

            // 3. 为每一个找到的入口点DLL创建一个独立的加载上下文
            foreach (string entryPointDllPath in entryPointDlls)
            {
                try
                {
                    Logger.Info("[插件加载器] 发现入口点: {GetFileName} in plugin '{PluginName}'", Path.GetFileName(entryPointDllPath), plugin.Name);

                    // 4. 创建独立的、可回收的加载上下文，并使用入口点自己的路径来初始化解析器。
                    var loadContext = new PluginAssemblyLoadContext(entryPointDllPath);

                    // 将创建的上下文存入我们的静态字典中
                    PluginLoadContexts[entryPointDllPath] = loadContext;

                    // 5. 使用上下文加载入口点程序集。
                    //    上下文会自动处理此程序集后续请求的所有托管和非托管依赖。
                    var assembly =
                        loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(entryPointDllPath)));

                    loadedPluginAssemblies.Add(assembly);

                    // 6. 在刚刚加载的入口点程序集中查找模块实现
                    var moduleTypes = assembly.GetTypes()
                        .Where(t => typeof(IProgramModule).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

                    bool foundModuleInAssembly = false;
                    foreach (var type in moduleTypes)
                    {
                        if (Activator.CreateInstance(type) is not IProgramModule pluginModuleInstance)
                            continue;

                        pluginModules.Add(pluginModuleInstance);
                        Logger.Info("[插件加载器] -> 成功加载模块 '{TypeFullName}'", type.FullName);
                        foundModuleInAssembly = true;
                    }

                    if (!foundModuleInAssembly)
                        Logger.Info("[插件加载器] -> 入口点 '{GetFileName}' 中未发现 IProgramModule 实现。", Path.GetFileName(entryPointDllPath));
                }
                catch (Exception ex)
                {
                    Logger.Info("[插件加载器] 严重错误: 加载入口点 '{GetFileName}' 失败. {Exception}", Path.GetFileName(entryPointDllPath), ex);
                }
            }
        }

        // 8. 合并内置模块和插件模块
        var allModules = coreModules.Concat(pluginModules).Distinct().ToList();
        AllModules = allModules;
        return (allModules, loadedPluginAssemblies.Distinct().ToList());
    }

    /// <summary>
    /// 当默认的 AssemblyLoadContext 无法解析程序集时，此方法将被调用。
    /// 它会轮询我们所有的插件上下文，看看是否有任何一个可以提供所需的程序集。
    /// </summary>
    private static Assembly? ResolvePluginDependency(AssemblyLoadContext defaultContext, AssemblyName assemblyName)
    {
        // 获取当前线程的解析跟踪字典，如果不存在则创建一个
        ResolvingAssemblies.Value ??= new ConcurrentDictionary<string, bool>();
        // 【卫兵检查】如果当前线程已经正在尝试解析这个程序集，则立即返回 null 以打破循环。
        if (!ResolvingAssemblies.Value.TryAdd(assemblyName.FullName, true))
        {
            // TryAdd 失败意味着 key 已经存在，说明我们陷入了递归
            return null;
        }

        try
        {
            Logger.Info("[依赖解析] 默认上下文无法找到 '{AssemblyNameFullName}'。正在询问插件上下文...", assemblyName.FullName);

            // 遍历我们为插件创建的所有加载上下文
            foreach (var context in PluginLoadContexts.Values)
            {
                // 尝试让每个插件上下文使用其自己的解析逻辑来加载这个程序集
                // 这会触发我们重写的 Load(AssemblyName) 方法
                var assembly = context.LoadFromAssemblyName(assemblyName);
                Logger.Info("[依赖解析] -> 成功！插件上下文 '{ContextName}' 提供了 '{AssemblyNameName}'。", context.Name, assemblyName.Name);
                return assembly;
            }

            Logger.Info("[依赖解析] -> 没有任何插件上下文能够提供 '{AssemblyNameName}'。", assemblyName.Name);
            return null; // 返回 null，表示我们也无能为力
        }
        catch (Exception)
        {
            // 如果一个上下文找不到它，这很正常，它会抛出异常。我们忽略它，继续尝试下一个。
        }
        finally
        {
            // 【清理】无论成功与否，在退出此方法前，必须将程序集从跟踪字典中移除。
            ResolvingAssemblies.Value.TryRemove(assemblyName.FullName, out _);
        }
        return null;
    }


    private static IReadOnlyList<IProgramModule> CoreModules { get; } =
    [
        new AiServiceConfigModule(),
        new WorkflowConfigModule(),
        new WorkflowTestModule(),
        new AuthenticationModule(),
        new FrontendHostModule(),
        new PlayerSaveModule()
    ];

    [field: AllowNull, MaybeNull]
    private static IReadOnlyList<IProgramModule> AllModules
    {
        get => field ?? CoreModules;
        set;
    }

    /// <summary>
    /// 获取主要程序集中实现了指定接口 {T} 的模块。（用于自动类型生成等）
    /// </summary>
    public static IEnumerable<T> GetAllModules<T>() => AllModules.OfType<T>();

    /// <summary>
    /// 对实现了指定接口 {T} 的模块列表执行一个操作。
    /// </summary>
    public static void ForEachModules<T>(this IEnumerable<IProgramModule> modules, Action<T> action)
    {
        foreach (var module in modules.OfType<T>())
        {
            action(module);
        }
    }
}