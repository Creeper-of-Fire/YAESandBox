using System.Reflection;
using DotNetEnv;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.AppWeb;
using YAESandBox.Authentication;
using YAESandBox.Core.API;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;
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
var (allModules, pluginAssemblies) = ApplicationModules.DiscoverAndLoadAllModules(builder.Environment, builder.Configuration);

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

// --- Application Services (Singleton or Scoped depending on need) ---
builder.Services.AddSingleton<IGeneralJsonStorage, JsonFileCacheJsonStorage>(_ =>
    new JsonFileCacheJsonStorage(builder.Configuration.GetValue<string?>("DataFiles:RootDirectory")));

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

builder.Services.AddSingleton<IPluginAssetService, PluginAssetService>();

var app = builder.Build();

// 配置中间件
// =================== 统一插件静态文件挂载 ===================
// TODO: [PluginManagement] 考虑在多插件场景下，处理前端组件命名冲突问题。
//  - 方案1: 强制约定插件组件名称需以插件名作为前缀。
//  - 方案2: 后端在DiscoverDynamicAssets时，扫描所有插件声明的组件名，
//           若有冲突，则抛出错误或自动重命名Schema中的x-vue-component/x-web-component指令值。
//  - 方案3: 前端插件加载器对不同插件的同名组件进行命名空间隔离。
//  目前，假定所有插件组件名称在全局范围内是唯一的。
string pluginsRelativePath = app.Configuration.GetValue<string>("Plugins:RootPath") ?? "Plugins";
string pluginsRootPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, pluginsRelativePath));
if (Directory.Exists(pluginsRootPath))
{
    foreach (string pluginDir in Directory.GetDirectories(pluginsRootPath))
    {
        string pluginName = new DirectoryInfo(pluginDir).Name;
        string pluginWwwRootPath = Path.Combine(pluginDir, "wwwroot");

        if (Directory.Exists(pluginWwwRootPath))
        {
            // 约定：所有插件的静态资源都通过 /plugins/{PluginName} 访问
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(pluginWwwRootPath),
                RequestPath = $"/plugins/{pluginName}"
            });

            Console.WriteLine($"[静态文件服务] 已为插件 '{pluginName}' 挂载 wwwroot: '{pluginWwwRootPath}' -> '/plugins/{pluginName}'");
        }
    }
}
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
        public void Apply(OpenApiSchema schema,
            SchemaFilterContext context)
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
        /// <summary>
        /// 发现并加载所有模块，包括内置模块和来自插件目录的动态模块。
        /// </summary>
        /// <param name="environment">Web主机环境，用于定位插件目录。</param>
        /// <returns>一个包含所有模块实例和加载的插件程序集的元组。</returns>
        public static (IReadOnlyList<IProgramModule> Modules, IReadOnlyList<Assembly> PluginAssemblies)
            DiscoverAndLoadAllModules(IWebHostEnvironment environment, IConfiguration configuration)
        {
            // 1. 定义内置的核心模块列表
            var coreModules = new List<IProgramModule>
            {
                new CoreModule(),
                new AiServiceConfigModule(),
                new WorkflowConfigModule(),
                new WorkflowTestModule(),
                new AuthenticationModule()
            };

            var loadedPluginAssemblies = new List<Assembly>();
            var pluginModules = new List<IProgramModule>();

            // 2. 扫描插件目录
            string pluginsRelativePath = configuration.GetValue<string>("Plugins:RootPath") ?? "Plugins";
            string pluginsPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, pluginsRelativePath));

            Console.WriteLine($"[插件加载器] 正在扫描插件目录: {pluginsPath}");

            if (!Directory.Exists(pluginsPath))
            {
                Console.WriteLine($"[插件加载器] 插件目录不存在，跳过加载。");
                return (coreModules, loadedPluginAssemblies);
            }

            foreach (string pluginDir in Directory.GetDirectories(pluginsPath))
            {
                string pluginName = new DirectoryInfo(pluginDir).Name;
                string pluginDllPath = Path.Combine(pluginDir, $"{pluginName}.dll");

                if (!File.Exists(pluginDllPath))
                    continue;

                try
                {
                    // 3. 加载插件程序集
                    var assembly = Assembly.LoadFrom(pluginDllPath);
                    loadedPluginAssemblies.Add(assembly);

                    // 4. 在插件程序集中查找所有 IProgramModule 的实现
                    var moduleTypes = assembly.GetTypes()
                        .Where(t => typeof(IProgramModule).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

                    foreach (var type in moduleTypes)
                    {
                        // 5. 实例化并添加到插件模块列表
                        if (Activator.CreateInstance(type) is not IProgramModule pluginModuleInstance)
                            continue;
                        pluginModules.Add(pluginModuleInstance);
                        Console.WriteLine($"[插件加载器] 成功加载模块 '{type.Name}' (来自插件: {pluginName})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[插件加载器] 错误: 加载插件 {pluginName} 失败. {ex.Message}");
                }
            }

            // 6. 合并内置模块和插件模块
            var allModules = coreModules.Concat(pluginModules).ToList();

            return (allModules, loadedPluginAssemblies);
        }

        private static IEnumerable<IProgramModule> Modules { get; } =
        [
            new CoreModule(),
            new AiServiceConfigModule(),
            new WorkflowConfigModule(),
            new WorkflowTestModule(),
            new AuthenticationModule()
        ];

        /// <summary>
        /// 获取所有实现了指定接口 {T} 的模块。
        /// </summary>
        public static IEnumerable<T> GetModules<T>()
        {
            return Modules.OfType<T>();
        }

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

        // /// <summary>
        // /// 对所有实现了指定接口 {T} 的模块执行一个操作。
        // /// </summary>
        // public static void ForEachModules<T>(Action<T> action)
        // {
        //     foreach (var module in Modules.OfType<T>())
        //     {
        //         action(module);
        //     }
        // }
    }
}