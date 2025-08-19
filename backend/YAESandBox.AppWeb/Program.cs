using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using DotNetEnv;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.AppWeb;
using YAESandBox.Authentication;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;
using YAESandBox.Depend.AspNetCore.Secret;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService.API;
using YAESandBox.Workflow.API;
using YAESandBox.Workflow.Test.API;

Env.Load();
var builder = WebApplication.CreateBuilder(args);


// 1. 定义 CORS 策略名称 (可以自定义)
const string myAllowSpecificOrigins = "_myAllowSpecificOrigins";

// 2. 添加 CORS 服务，并配置策略
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins(
                    // *** 把这里替换成你的 Vite 前端开发服务器的确切地址! ***
                    "http://localhost:4173", // 示例 Vite 默认端口
                    "http://127.0.0.1:4173", // 有时也需要加上 127.0.0.1
                    "http://localhost:5173", // 示例 Vite 默认端口
                    "http://127.0.0.1:5173" // 有时也需要加上 127.0.0.1
                    // 如果你的前端运行在其他地址，也要加进去
                )
                .AllowAnyHeader() // 允许所有请求头
                .AllowAnyMethod() // 允许所有 HTTP 方法 (GET, POST, PUT, etc.)
                .AllowCredentials(); // *** SignalR 必须允许凭据 ***
        });
});

// 在程序启动早期就发现和加载所有模块（内置+插件）
// ** 手动创建发现服务实例，用于模块加载 **
var pluginDiscoveryService = new DefaultPluginDiscoveryService(builder.Environment, builder.Configuration);
var (allModules, pluginAssemblies) = ApplicationModules.DiscoverAndLoadAllModules(pluginDiscoveryService);

// 添加 MVC 服务
var mvcBuilder = builder.Services.AddControllers()
    .AddJsonOptions(options => // Configure JSON options
    {
        // Serialize enums as strings in requests/responses
        YaeSandBoxJsonHelper.CopyFrom(options.JsonSerializerOptions, YaeSandBoxJsonHelper.JsonSerializerOptions);
    });

// 告诉 MVC 框架，停止默认的全局扫描行为。
// 我们将手动告诉它去哪里找控制器。
mvcBuilder.PartManager.ApplicationParts.Clear();

// 将 allModules 列表封装到 ModuleProvider 中，并将其注册为单例服务。
// 这样，应用程序的任何部分都可以通过注入 IModuleProvider 来访问所有模块。
builder.Services.AddSingleton<IModuleProvider>(new ModuleProvider(allModules));

// 循环配置 MVC 部件
allModules.ForEachModules<IProgramModuleMvcConfigurator>(it => it.ConfigureMvc(mvcBuilder));

// 注册 DI 服务和其他杂项
allModules.ForEachModules<IProgramModule>(it => it.RegisterServices(builder.Services));

// --- OpenAPI / Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // --- 告诉 Swashbuckle 如何根据 GroupName 分配 API ---
    // 如果 API 没有明确的 GroupName，默认可以将其分配给公开文档
    options.DocInclusionPredicate((docName, apiDesc) =>
        {
            if (apiDesc.GroupName != null)
                // 核心规则：API 的 GroupName 必须精确匹配当前要生成的文档名。
                return apiDesc.GroupName == docName;

            Console.WriteLine($"没有为{apiDesc.HttpMethod}定义文档");
            return false;
        }
    );

    // Add Enum Schema Filter to display enums as strings in Swagger UI
    options.SchemaFilter<EnumSchemaFilter>(); // 假设 EnumSchemaFilter 已定义

    options.DocumentFilter<SignalRDtoDocumentFilter>();
});

builder.Services.AddHttpClient();

// --- SignalR ---
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        YaeSandBoxJsonHelper.CopyFrom(options.PayloadSerializerOptions, YaeSandBoxJsonHelper.JsonSerializerOptions);
    });

// ---- 数据保护服务 ---
// 1. 添加数据保护服务
builder.Services.AddDataProtection();
//.PersistKeysToFileSystem(new DirectoryInfo(@"/path/to/keys")) // 在生产环境中配置密钥存储位置

// 2. 将 SecretProtector 注册为单例
// 它现在自己处理 IDataProtectionProvider 的依赖
builder.Services.AddSingleton<ISecretProtector, SecretProtector>();

// 3. 注册通用的数据保护服务
builder.Services.AddSingleton<IDataProtectionService, DataProtectionService>();

// --- 存储服务 ---
// 1. 将最内层的、带缓存的存储服务注册为一个具体的类型。
//    它本身也是一个单例。
builder.Services.AddSingleton(
    new JsonFileCacheJsonStorage(builder.Configuration.GetValue<string?>("DataFiles:RootDirectory"))
);

// 2. 注册我们的装饰器。这才是最终提供给应用程序的服务。
//    使用工厂模式来确保正确的依赖被注入到装饰器的构造函数中。
builder.Services.AddSingleton<IGeneralJsonRootStorage>(sp =>
    new ProtectedJsonStorage(
        sp.GetRequiredService<JsonFileCacheJsonStorage>(), // <-- 明确告诉容器，我要用那个具体的JsonFileCacheJsonStorage来装饰
        sp.GetRequiredService<IDataProtectionService>() // <-- 容器自动提供数据保护服务
    )
);

// --- CORS (Configure as needed, especially for development) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
        // If using SignalR with credentials, adjust AllowAnyOrigin and add AllowCredentials()
        // policy.WithOrigins("http://localhost:xxxx") // Your frontend URL
        //       .AllowAnyMethod()
        //       .AllowAnyHeader()
        //       .AllowCredentials();
    });
});


allModules.ForEachModules<IProgramModuleWithInitialization>(it =>
    it.Initialize(new ModuleInitializationContext(allModules, pluginAssemblies)));

builder.Services.AddSingleton<IPluginDiscoveryService>(pluginDiscoveryService);

var app = builder.Build();

// 配置中间件
// =================== 统一插件静态文件挂载 ===================
allModules.ForEachModules<IProgramModuleStaticAssetConfigurator>(it =>
    it.ConfigureStaticAssets(app, app.Environment));
// =========================================================

allModules.ForEachModules<IProgramModuleAppConfigurator>(it => it.ConfigureApp(app));

app.UseDefaultFiles(); // 使其查找 wwwroot 中的 index.html 或 default.html (可选，但良好实践)
app.UseStaticFiles(); // 启用从 wwwroot 提供静态文件的功能

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); // 启用 Swagger 中间件 (提供 JSON)
    app.UseSwaggerUI(c =>
    {
        // 找到所有实现了 ISwaggerUIOptionsConfigurator 接口的模块，循环应用它们的配置
        allModules.ForEachModules<IProgramModuleSwaggerUiOptionsConfigurator>(it => it.ConfigureSwaggerUi(c));

        // (可选) 设置默认展开级别等 UI 选项
        c.DefaultModelsExpandDepth(-1); // 折叠模型定义
        c.DocExpansion(DocExpansion.List); // 列表形式展开操作
    });
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection();


app.UseRouting(); // Add routing middleware

app.UseCors(myAllowSpecificOrigins); // Apply CORS policy - place before UseAuthorization/UseEndpoints

app.UseAuthentication(); // <-- 先认证
app.UseAuthorization(); // <-- 后授权

app.MapControllers(); // Map attribute-routed controllers

// 聚合映射所有模块提供的 Hub
allModules.ForEachModules<IProgramModuleHubRegistrar>(it => it.MapHubs(app));

app.Run();

namespace YAESandBox.AppWeb
{
    // --- Records/Classes used in Program.cs ---

    // Helper class for Swagger Enum Display
    /// <inheritdoc />
    internal class EnumSchemaFilter : ISchemaFilter
    {
        /// <inheritdoc />
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (!context.Type.IsEnum) return;
            schema.Enum.Clear();
            schema.Type = "string"; // Represent enum as string
            schema.Format = null;
            foreach (string enumName in Enum.GetNames(context.Type)) schema.Enum.Add(new OpenApiString(enumName));
        }
    }

    internal static class ApplicationModules
    {
        // 一个静态字典来持有我们创建的所有插件加载上下文
        private static readonly Dictionary<string, PluginAssemblyLoadContext> PluginLoadContexts = new();
        private static bool _isResolvingEventHooked = false;

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
                Console.WriteLine("[插件加载器] 已挂载默认上下文的程序集解析事件。");
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
                        Console.WriteLine($"[插件加载器] 警告: 插件 '{plugin.Name}' 目录中存在DLL，但未找到任何带有 .deps.json 的入口点程序集。");
                    continue;
                }

                // 3. 为每一个找到的入口点DLL创建一个独立的加载上下文
                foreach (string entryPointDllPath in entryPointDlls)
                {
                    try
                    {
                        Console.WriteLine($"[插件加载器] 发现入口点: {Path.GetFileName(entryPointDllPath)} in plugin '{plugin.Name}'");

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
                            .Where(t => typeof(IProgramModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                        bool foundModuleInAssembly = false;
                        foreach (var type in moduleTypes)
                        {
                            if (Activator.CreateInstance(type) is not IProgramModule pluginModuleInstance)
                                continue;

                            pluginModules.Add(pluginModuleInstance);
                            Console.WriteLine($"[插件加载器] -> 成功加载模块 '{type.FullName}'");
                            foundModuleInAssembly = true;
                        }

                        if (!foundModuleInAssembly)
                            Console.WriteLine($"[插件加载器] -> 入口点 '{Path.GetFileName(entryPointDllPath)}' 中未发现 IProgramModule 实现。");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[插件加载器] 严重错误: 加载入口点 '{Path.GetFileName(entryPointDllPath)}' 失败. {ex}");
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
            Console.WriteLine($"[依赖解析] 默认上下文无法找到 '{assemblyName.FullName}'。正在询问插件上下文...");

            // 遍历我们为插件创建的所有加载上下文
            foreach (var context in PluginLoadContexts.Values)
            {
                try
                {
                    // 尝试让每个插件上下文使用其自己的解析逻辑来加载这个程序集
                    // 这会触发我们重写的 Load(AssemblyName) 方法
                    var assembly = context.LoadFromAssemblyName(assemblyName);
                    Console.WriteLine($"[依赖解析] -> 成功！插件上下文 '{context.Name}' 提供了 '{assemblyName.Name}'。");
                    return assembly;
                }
                catch (Exception)
                {
                    // 如果一个上下文找不到它，这很正常，它会抛出异常。我们忽略它，继续尝试下一个。
                }
            }

            Console.WriteLine($"[依赖解析] -> 没有任何插件上下文能够提供 '{assemblyName.Name}'。");
            return null; // 返回 null，表示我们也无能为力
        }

        private static IReadOnlyList<IProgramModule> CoreModules { get; } =
        [
            new AiServiceConfigModule(),
            new WorkflowConfigModule(),
            new WorkflowTestModule(),
            new AuthenticationModule()
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
}