using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.AppWeb;
using YAESandBox.Core.API;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService.API;
using YAESandBox.Workflow.API;

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

// --- 手动 new 模块实例，保留它们以备后用 ---


// 添加 MVC 服务
var mvcBuilder = builder.Services.AddControllers()
    .AddJsonOptions(options => // Configure JSON options
    {
        // Serialize enums as strings in requests/responses
        YaeSandBoxJsonHelper.CopyFrom(options.JsonSerializerOptions, YaeSandBoxJsonHelper.JsonSerializerOptions);
    });

// 关键：告诉 MVC 框架，停止默认的全局扫描行为。
// 我们将手动告诉它去哪里找控制器。
mvcBuilder.PartManager.ApplicationParts.Clear();

// 循环配置 MVC 部件
ApplicationModules.ForEachModules<IProgramModuleMvcConfigurator>(it => it.ConfigureMvc(mvcBuilder));

// 注册 DI 服务和其他杂项
ApplicationModules.ForEachModules<IProgramModule>(it => it.RegisterServices(builder.Services));

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


var app = builder.Build();

app.UseDefaultFiles(); // 使其查找 wwwroot 中的 index.html 或 default.html (可选，但良好实践)
app.UseStaticFiles(); // 启用从 wwwroot 提供静态文件的功能

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); // 启用 Swagger 中间件 (提供 JSON)
    app.UseSwaggerUI(c =>
    {
        // 找到所有实现了 ISwaggerUIOptionsConfigurator 接口的模块，循环应用它们的配置
        ApplicationModules.ForEachModules<IProgramModuleSwaggerUiOptionsConfigurator>(it => it.ConfigureSwaggerUi(c));

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

app.UseAuthorization(); // Add authorization middleware if needed

app.MapControllers(); // Map attribute-routed controllers

// 聚合映射所有模块提供的 Hub
ApplicationModules.ForEachModules<IProgramModuleHubRegistrar>(it => it.MapHubs(app));

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
        private static IEnumerable<IProgramModule> Modules { get; } =
        [
            new CoreModule(),
            new AiServiceConfigModule(),
            new WorkflowConfigModule(),
        ];

        /// <summary>
        /// 获取所有实现了指定接口 {T} 的模块。
        /// </summary>
        public static IEnumerable<T> GetModules<T>()
        {
            return Modules.OfType<T>();
        }

        /// <summary>
        /// 对所有实现了指定接口 {T} 的模块执行一个操作。
        /// </summary>
        public static void ForEachModules<T>(Action<T> action)
        {
            foreach (var module in Modules.OfType<T>())
            {
                action(module);
            }
        }
    }
}