// --- START OF FILE Program.cs ---

using System.Reflection;
using Microsoft.OpenApi.Models; // Needed for AddOpenApi
// Needed for WebApplicationBuilder
// Needed for IServiceCollection extensions
// Needed for IHostEnvironment
using YAESandBox.API.Hubs; // For GameHub
using YAESandBox.API.Services; // For BlockManager, NotifierService, WorkflowService
using System.Text.Json.Serialization;
using Swashbuckle.AspNetCore.SwaggerGen;
using YAESandBox.API;
using YAESandBox.API.DTOs;
using YAESandBox.Core.Block;
using static GlobalSwaggerConstants;


var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options => // Configure JSON options
    {
        // Serialize enums as strings in requests/responses
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// --- OpenAPI / Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // --- 定义公开 API 文档 ---
    options.SwaggerDoc(PublicApiGroupName, new OpenApiInfo
    {
        Title = "YAESandBox API (Public)",
        Version = "v1",
        Description = "供前端和外部使用的公开 API 文档。"
    });

    // --- 定义内部/调试 API 文档 ---
    options.SwaggerDoc(InternalApiGroupName, new OpenApiInfo
    {
        Title = "YAESandBox API (Internal)",
        Version = "v1",
        Description = "包含所有 API，包括内部调试接口。"
    });

    // --- 告诉 Swashbuckle 如何根据 GroupName 分配 API ---
    // 如果 API 没有明确的 GroupName，默认可以将其分配给公开文档
    options.DocInclusionPredicate((docName, apiDesc) =>
        {
            // 如果 API 没有 GroupName 设置，我们默认认为它属于 Public
            string groupName = apiDesc.GroupName ?? PublicApiGroupName;

            // 只有当 API 的 GroupName 与当前生成的文档名称匹配时，才包含它
            // 或者，如果当前生成的是 Internal 文档，包含所有 API (Public + Internal)
            if (docName == InternalApiGroupName) // 内部文档包含所有公共 API 和标记为 internal 的 API
                return groupName is PublicApiGroupName or InternalApiGroupName;
            // 否则，生成的是 Public 文档
            // 公开文档只包含 GroupName 为 Public 的 API
            return groupName == PublicApiGroupName;
        }
    );


    // --- 加载 XML 注释文件 ---

    // 1. 加载 API 项目自身的 XML 注释文件
    options.AddSwaggerDocumentation(Assembly.GetExecutingAssembly());

    // 2. 加载 Service 项目的 XML 注释文件
    options.AddSwaggerDocumentation(typeof(BasicBlockService).Assembly);

    // 3. 加载共享 DTO 项目的 XML 注释文件
    options.AddSwaggerDocumentation(typeof(AtomicOperationRequestDto).Assembly);

    // Add Enum Schema Filter to display enums as strings in Swagger UI
    options.SchemaFilter<EnumSchemaFilter>(); // 假设 EnumSchemaFilter 已定义
    
    options.DocumentFilter<SignalRDtoDocumentFilter>();
});


// --- SignalR ---
builder.Services.AddSignalR()
    .AddJsonProtocol(options => // Use System.Text.Json for SignalR
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// --- Application Services (Singleton or Scoped depending on need) ---
// NotifierService depends on IHubContext, usually Singleton
builder.Services.AddSingleton<INotifierService, SignalRNotifierService>();

// BlockManager holds state, make it Singleton. Depends on INotifierService.
builder.Services.AddSingleton<IBlockManager, BlockManager>();

// WorkflowService depends on IBlockManager and INotifierService, make it Singleton or Scoped.
// Singleton is fine if it doesn't hold per-request state.
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();
builder.Services.AddSingleton<IBlockManagementService, BlockManagementService>();
builder.Services.AddSingleton<IBlockWritService, BlockWritService>();
builder.Services.AddSingleton<IBlockReadService, BlockReadService>();


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
        // --- 配置 UI 以显示两个文档版本 ---
        // 端点 1: 公开 API
        c.SwaggerEndpoint($"/swagger/{PublicApiGroupName}/swagger.json", $"YAESandBox API (Public)");
        // 端点 2: 内部 API
        c.SwaggerEndpoint($"/swagger/{InternalApiGroupName}/swagger.json", $"YAESandBox API (Internal)");

        // (可选) 设置默认展开级别等 UI 选项
        c.DefaultModelsExpandDepth(-1); // 折叠模型定义
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List); // 列表形式展开操作
    });
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection();

app.UseRouting(); // Add routing middleware

app.UseCors("AllowAll"); // Apply CORS policy - place before UseAuthorization/UseEndpoints

app.UseAuthorization(); // Add authorization middleware if needed

app.MapControllers(); // Map attribute-routed controllers

// Map SignalR Hub
app.MapHub<GameHub>("/gamehub"); // Define the SignalR endpoint URL

// Minimal API example (keep or remove)
// app.MapGet("/weatherforecast", () =>
//     {
//         var forecast = Enumerable.Range(1, 5).Select(index =>
//                 new WeatherForecast
//                 (
//                     DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
//                     Random.Shared.Next(-20, 55),
//                     "Sample" // Simplified
//                 ))
//             .ToArray();
//         return forecast;
//     })
//     .WithName("GetWeatherForecast")
//     .RequireCors("AllowAll"); // Apply CORS to minimal APIs too

app.Run();

// --- Records/Classes used in Program.cs ---
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}

// Helper class for Swagger Enum Display
public class EnumSchemaFilter : Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter
{
    public void Apply(OpenApiSchema schema,
        Swashbuckle.AspNetCore.SwaggerGen.SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            schema.Enum.Clear();
            schema.Type = "string"; // Represent enum as string
            schema.Format = null;
            foreach (string enumName in Enum.GetNames(context.Type))
            {
                schema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString(enumName));
            }
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