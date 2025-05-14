// --- START OF FILE Program.cs ---

using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.API;
using YAESandBox.API.DTOs;
using YAESandBox.API.Hubs;
using YAESandBox.API.Services;
using YAESandBox.API.Services.InterFaceAndBasic;
using YAESandBox.API.Services.WorkFlow;
using YAESandBox.Core.Block;
using YAESandBox.Depend;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService.ConfigManagement;
using YAESandBox.Workflow.AIService.Controller;

var builder = WebApplication.CreateBuilder(args);


// 1. 定义 CORS 策略名称 (可以自定义)
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

// 2. 添加 CORS 服务，并配置策略
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
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

// Add services to the container.
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AiConfigurationsController).Assembly)
    .AddJsonOptions(options => // Configure JSON options
    {
        // Serialize enums as strings in requests/responses
        YAESandBoxJsonHelper.CopyFrom(options.JsonSerializerOptions, YAESandBoxJsonHelper.JsonSerializerOptions);
    });

// --- OpenAPI / Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // --- 定义公开 API 文档 ---
    options.SwaggerDoc(GlobalSwaggerConstants.PublicApiGroupName, new OpenApiInfo
    {
        Title = "YAESandBox API (Public)",
        Version = "v1",
        Description = "供前端和外部使用的公开 API 文档。"
    });

    // --- 定义内部/调试 API 文档 ---
    options.SwaggerDoc(GlobalSwaggerConstants.InternalApiGroupName, new OpenApiInfo
    {
        Title = "YAESandBox API (Internal)",
        Version = "v1",
        Description = "包含所有 API，包括内部调试接口。"
    });

    // --- 定义 AiService 模块 API 文档 ---
    options.SwaggerDoc(AiConfigurationsController.AiConfigGroupName, new OpenApiInfo
    {
        Title = "YAESandBox API (AI Config)",
        Version = "v1",
        Description = "包含AI服务配置相关的API。"
    });


    // --- 告诉 Swashbuckle 如何根据 GroupName 分配 API ---
    // 如果 API 没有明确的 GroupName，默认可以将其分配给公开文档
    options.DocInclusionPredicate((docName, apiDesc) =>
        {
            // 如果 API 没有 GroupName 设置，我们默认认为它属于 Public
            string groupName = apiDesc.GroupName ?? GlobalSwaggerConstants.PublicApiGroupName;

            // 只有当 API 的 GroupName 与当前生成的文档名称匹配时，才包含它
            // 或者，如果当前生成的是 Internal 文档，包含所有 API (Public + Internal)
            if (docName == GlobalSwaggerConstants.InternalApiGroupName) // 内部文档包含所有公共 API 和标记为 internal 的 API
                return groupName is GlobalSwaggerConstants.PublicApiGroupName or GlobalSwaggerConstants.InternalApiGroupName;
            // 新增对 AiConfig 的处理
            if (docName == AiConfigurationsController.AiConfigGroupName)
                return apiDesc.GroupName == AiConfigurationsController.AiConfigGroupName;

            // 否则，生成的是 Public 文档
            // 公开文档只包含 GroupName 为 Public 的 API
            return groupName == GlobalSwaggerConstants.PublicApiGroupName;
        }
    );


    // --- 加载 XML 注释文件 ---

    // 1. 加载 API 项目自身的 XML 注释文件
    options.AddSwaggerDocumentation(Assembly.GetExecutingAssembly());

    // 2. 加载 Service 项目的 XML 注释文件
    options.AddSwaggerDocumentation(typeof(BasicBlockService).Assembly);

    // 3. 加载共享 DTO 项目的 XML 注释文件
    options.AddSwaggerDocumentation(typeof(AtomicOperationRequestDto).Assembly);

    options.AddSwaggerDocumentation(typeof(BlockResultCode).Assembly);

    options.AddSwaggerDocumentation(typeof(AiConfigurationsController).Assembly);

    // Add Enum Schema Filter to display enums as strings in Swagger UI
    options.SchemaFilter<EnumSchemaFilter>(); // 假设 EnumSchemaFilter 已定义

    options.DocumentFilter<SignalRDtoDocumentFilter>();
});

builder.Services.AddHttpClient();

// --- SignalR ---
builder.Services.AddSignalR()
    .AddJsonProtocol(options => // Use System.Text.Json for SignalR
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// --- Application Services (Singleton or Scoped depending on need) ---

// BlockManager holds state, make it Singleton.
builder.Services.AddSingleton<IBlockManager, BlockManager>();

// NotifierService depends on IHubContext, BlockManager, usually Singleton
builder.Services.AddSingleton<SignalRNotifierService>()
    .AddSingleton<INotifierService>(sp => sp.GetRequiredService<SignalRNotifierService>())
    .AddSingleton<IWorkflowNotifierService>(sp => sp.GetRequiredService<SignalRNotifierService>());

// WorkflowService depends on IBlockManager and INotifierService, make it Singleton or Scoped.
// Singleton is fine if it doesn't hold per-request state.
builder.Services.AddSingleton<IWorkFlowBlockService, WorkFlowBlockService>();
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();
builder.Services.AddSingleton<IBlockManagementService, BlockManagementService>();
builder.Services.AddSingleton<IBlockWritService, BlockWritService>();
builder.Services.AddSingleton<IBlockReadService, BlockReadService>();
builder.Services.AddSingleton<IGeneralJsonStorage, JsonFileJsonStorage>(_ =>
    new JsonFileJsonStorage(builder.Configuration.GetValue<string?>("DataFiles:RootDirectory")));

// AiConfigManager
builder.Services.AddSingleton<JsonFileAiConfigurationManager>()
    .AddSingleton<IAiConfigurationManager>(sp => sp.GetRequiredService<JsonFileAiConfigurationManager>())
    .AddSingleton<IAiConfigurationProvider>(sp => sp.GetRequiredService<JsonFileAiConfigurationManager>());


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
        // --- 配置 UI 以显示文档版本 ---
        // 端点 1: 公开 API
        c.SwaggerEndpoint($"/swagger/{GlobalSwaggerConstants.PublicApiGroupName}/swagger.json", $"YAESandBox API (Public)");
        // 端点 2: 内部 API
        c.SwaggerEndpoint($"/swagger/{GlobalSwaggerConstants.InternalApiGroupName}/swagger.json", $"YAESandBox API (Internal)");
        // 端点 3: AI API
        c.SwaggerEndpoint($"/swagger/{AiConfigurationsController.AiConfigGroupName}/swagger.json", $"YAESandBox API (AI Config)");

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

app.UseCors(MyAllowSpecificOrigins); // Apply CORS policy - place before UseAuthorization/UseEndpoints

app.UseAuthorization(); // Add authorization middleware if needed

app.MapControllers(); // Map attribute-routed controllers

// Map SignalR Hub
app.MapHub<GameHub>("/gamehub"); // Define the SignalR endpoint URL

app.Run();

namespace YAESandBox.API
{
    // --- Records/Classes used in Program.cs ---

// Helper class for Swagger Enum Display
    public class EnumSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema,
            SchemaFilterContext context)
        {
            if (context.Type.IsEnum)
            {
                schema.Enum.Clear();
                schema.Type = "string"; // Represent enum as string
                schema.Format = null;
                foreach (string enumName in Enum.GetNames(context.Type)) schema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString(enumName));
            }
        }
    }

    public static class SwaggerHelper
    {
        public static void AddSwaggerDocumentation(this SwaggerGenOptions options, Assembly assembly)
        {
            try
            {
                string XmlFilename = $"{assembly.GetName().Name}.xml";
                string XmlFilePath = Path.Combine(AppContext.BaseDirectory, XmlFilename);
                if (File.Exists(XmlFilePath))
                {
                    options.IncludeXmlComments(XmlFilePath);
                    Console.WriteLine($"加载 XML 注释: {XmlFilePath}");
                }
                else
                {
                    Console.WriteLine($"警告: 未找到 XML 注释文件: {XmlFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载 Contracts XML 注释时出错: {ex.Message}");
            }
        }
    }

    public static class GlobalSwaggerConstants
    {
        // --- 定义文档名称常量 ---
        public const string PublicApiGroupName = "v1-public"; // 公开 API 文档
        public const string InternalApiGroupName = "v1-internal"; // 内部/调试 API 文档
    }
// --- END OF FILE Program.cs ---
}