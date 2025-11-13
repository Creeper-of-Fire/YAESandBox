using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNetEnv;
using Microsoft.AspNetCore.DataProtection;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.AppWeb;
using YAESandBox.AppWeb.Services;
using YAESandBox.Depend.AspNetCore.Secret;
using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Secret;
using YAESandBox.Depend.Services;
using YAESandBox.Depend.Storage;
using YAESandBox.ModuleSystem.Abstractions;
using YAESandBox.ModuleSystem.Abstractions.PluginDiscovery;
using YAESandBox.ModuleSystem.AspNet;
using YAESandBox.ModuleSystem.AspNet.Interface;
using static YAESandBox.AppWeb.ProgramStatic;

Console.OutputEncoding = Encoding.UTF8;
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);
AppLogging.InitializeInBoostrap();

// =========================================================================
// === 1. 环境与配置设置 (核心改造部分) ===
// =========================================================================

// 存储位置
var rootPathProvider = new AppRootPathProvider(builder.Environment);
builder.Services.AddSingleton<IRootPathProvider>(rootPathProvider);
string physicalAppRoot = rootPathProvider.RootPath;
string dataRelativePath = builder.Configuration.GetValue<string>("DataFiles:RootDirectory") ?? "Data";
string dataAbsolutePath = Path.GetFullPath(Path.Combine(physicalAppRoot, dataRelativePath));
Directory.CreateDirectory(dataAbsolutePath);

// 只在开发环境中加载 .env 文件
if (builder.Environment.IsDevelopment())
{
    Logger.Info("在开发模式下运行，寻找 .env 文件...");
    Env.Load();
}

// ** 安全密钥管理: 从 Data/secrets.json 加载或生成 **
string secretsFilePath = Path.Combine(dataAbsolutePath, "secrets.json");
var secretsConfigBuilder = new ConfigurationBuilder().AddJsonFile(secretsFilePath, optional: true, reloadOnChange: true);
var secretsConfig = secretsConfigBuilder.Build();

string? jwtKey = secretsConfig["Jwt:Key"];
string? dataProtectionKey = secretsConfig["DataProtectionKey"];
bool secretsWereGenerated = false;

if (string.IsNullOrEmpty(jwtKey))
{
    jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); // 256-bit key
    secretsWereGenerated = true;
}

if (string.IsNullOrEmpty(dataProtectionKey))
{
    dataProtectionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    secretsWereGenerated = true;
}

if (secretsWereGenerated)
{
    var newSecrets = new Dictionary<string, object>
    {
        ["Jwt:Key"] = jwtKey,
        ["DataProtectionKey"] = dataProtectionKey
    };
    string json = JsonSerializer.Serialize(newSecrets,
        new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(secretsFilePath, json);
    Logger.Info("新的安全密钥存储并且保存到：{SecretsFilePath}", secretsFilePath);
}

// ** 将安全密钥动态添加到应用程序的整体配置中 **
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    { "Jwt:Key", jwtKey },
    { "DataProtectionKey", dataProtectionKey }
});

// =========================================================================
// === 3. 服务注册 ===
// =========================================================================

// 在程序启动早期就发现和加载所有模块（内置+插件）
// ** 手动创建发现服务实例，用于模块加载 **
string pluginsRelativePath = builder.Configuration.GetValue<string>("Plugins:RootPath") ?? "Plugins";
string pluginsAbsolutePath = Path.Combine(physicalAppRoot, pluginsRelativePath);
var pluginDiscoveryService = new DefaultPluginDiscoveryService(pluginsAbsolutePath);
var (allModules, pluginAssemblies) = ApplicationModules.DiscoverAndLoadAllModules(pluginDiscoveryService);


const string devPipeLine = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: devPipeLine, policy =>
    {
        policy.WithOrigins(
                "http://localhost:4173",
                "http://127.0.0.1:4173",
                "http://localhost:5173",
                "http://127.0.0.1:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});


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
builder.Services.AddSingleton<IPluginDiscoveryService>(pluginDiscoveryService);

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

            Logger.Info("没有为{ApiDescHttpMethod}定义文档", apiDesc.HttpMethod);
            return false;
        }
    );

    options.DocumentFilter<AdditionalSchemaDocumentFilter>();
});

builder.Services.AddHttpClient();

// --- SignalR ---
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        YaeSandBoxJsonHelper.CopyFrom(options.PayloadSerializerOptions, YaeSandBoxJsonHelper.JsonSerializerOptions);
    });

// --- 数据保护服务 ---
// 这将自动从 IConfiguration 读取 "DataProtectionKey"
string dataProtectionAbsolutePath = Path.Combine(dataAbsolutePath, "DataProtection-Keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionAbsolutePath))
    .SetApplicationName("YAESandBox"); // 为数据保护设置一个唯一的应用名
Directory.CreateDirectory(dataProtectionAbsolutePath); // 确保目录存在
builder.Services.AddSingleton<ISecretProtector, SecretProtector>();
builder.Services.AddSingleton<IDataProtectionService, DataProtectionService>();

// --- 存储服务 ---
// 将最内层的、带缓存的存储服务注册为一个具体的类型。
//    它本身也是一个单例。
builder.Services.AddSingleton(
    new JsonFileCacheJsonStorage(dataAbsolutePath)
);

// 注册我们的装饰器。这才是最终提供给应用程序的服务。
//    使用工厂模式来确保正确的依赖被注入到装饰器的构造函数中。
builder.Services.AddSingleton<IGeneralJsonRootStorage>(sp =>
    new ProtectedJsonStorage(
        sp.GetRequiredService<JsonFileCacheJsonStorage>(), // <-- 明确告诉容器，我要用那个具体的JsonFileCacheJsonStorage来装饰
        sp.GetRequiredService<IDataProtectionService>() // <-- 容器自动提供数据保护服务
    )
);

// --- 模块初始化 ---
allModules.ForEachModules<IProgramModuleWithInitialization>(it =>
    it.Initialize(new ModuleInitializationContext(allModules, pluginAssemblies)));

var app = builder.Build();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
AppLogging.Initialize(loggerFactory);

// =========================================================================
// === 4. 构建 WebApplication & 配置中间件管道 ===
// =========================================================================

// 配置中间件
// =================== 统一插件静态文件挂载 ===================
allModules.ForEachModules<IProgramModuleStaticAssetProvider>(it =>
    it.ConfigureStaticAssets(app));
// =========================================================

allModules.ForEachModules<IProgramModuleAppConfigurator>(it => it.ConfigureApp(app));

// --- Swagger UI 只在开发模式下启用 ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); // 启用 Swagger 中间件 (提供 JSON)
    app.UseSwaggerUI(c =>
    {
        // 找到所有实现了 ISwaggerUIOptionsConfigurator 接口的模块，循环应用它们的配置
        allModules.ForEachModules<IProgramModuleSwaggerUiOptionsConfigurator>(it => it.ConfigureSwaggerUi(c));

        // 设置默认展开级别等 UI 选项
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

// --- 启用 CORS 中间件 ---
// --- CORS ---
if (builder.Environment.IsDevelopment())
{
    app.UseCors(devPipeLine);
}

app.UseAuthentication(); // <-- 先认证
app.UseAuthorization(); // <-- 后授权

app.MapControllers(); // Map attribute-routed controllers

// 聚合映射所有模块提供的 Hub
allModules.ForEachModules<IProgramModuleHubRegistrar>(it => it.MapHubs(app));

// --- 模块最后配置 ---
var finalContext = new FinalConfigurationContext(app, app);
allModules.ForEachModules<IProgramModuleAtLastConfigurator>(it => it.ConfigureAtLast(finalContext));
app.Run();

namespace YAESandBox.AppWeb
{
    file static class ProgramStatic
    {
        internal static IAppLogger Logger { get; } = AppLogging.CreateLogger<Program>();
    }
}