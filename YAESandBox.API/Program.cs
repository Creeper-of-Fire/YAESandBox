// --- START OF FILE Program.cs ---

using Microsoft.OpenApi.Models; // Needed for AddOpenApi
using Microsoft.AspNetCore.Builder; // Needed for WebApplicationBuilder
using Microsoft.Extensions.DependencyInjection; // Needed for IServiceCollection extensions
using Microsoft.Extensions.Hosting; // Needed for IHostEnvironment
using YAESandBox.API.Hubs; // For GameHub
using YAESandBox.API.Services; // For BlockManager, NotifierService, WorkflowService
using System.Text.Json.Serialization;
using YAESandBox.Core.Block;
using YAESandBox.Core.State; // For EnumConverter

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options => // Configure JSON options
    {
        // Serialize enums as strings in requests/responses
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// --- OpenAPI / Swagger ---
builder.Services.AddEndpointsApiExplorer(); // Needed for Minimal APIs if used, and Swagger
builder.Services.AddSwaggerGen(c => // Configure Swagger
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "YAESandBox API", Version = "v1" });
    // Include XML comments if set up in .csproj file
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    // Add Enum Schema Filter to display enums as strings in Swagger UI
    c.SchemaFilter<EnumSchemaFilter>(); // Requires the EnumSchemaFilter class defined below
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
builder.Services.AddSingleton<IBlockWritService, BlockWritService>();
builder.Services.AddSingleton<IBlockReadService, BlockReadService>();

// WorkflowService depends on IBlockManager and INotifierService, make it Singleton or Scoped.
// Singleton is fine if it doesn't hold per-request state.
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();


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
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "YAESandBox API v1"));
    // app.UseDeveloperExceptionPage(); // Useful for debugging startup issues
}
else
{
    app.UseExceptionHandler("/Error"); // Add basic error handling page
    app.UseHsts(); // Enable HTTP Strict Transport Security
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
    public void Apply(Microsoft.OpenApi.Models.OpenApiSchema schema,
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
// --- END OF FILE Program.cs ---