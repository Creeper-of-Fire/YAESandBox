using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using YAESandBox.Depend;
using YAESandBox.Depend.AspNetCore;

namespace YAESandBox.AppWeb.Services;

/// <summary>
/// 一个 Swagger 文档过滤器，用于确保 SignalR 使用的 DTOs 被包含在 OpenAPI Schema 中，
/// 即使它们没有被任何 HTTP API Controller 直接引用。
/// 这对于依赖 Swagger Schema 生成 TypeScript 类型的前端代码生成器很有用。
/// </summary>
internal class SignalRDtoDocumentFilter(ISchemaGenerator schemaGenerator) : IDocumentFilter
{
    private static ILogger Logger { get; } = AppLogging.CreateLogger<SignalRDtoDocumentFilter>();
    private readonly ISchemaGenerator _schemaGenerator = schemaGenerator ?? throw new ArgumentNullException(nameof(schemaGenerator));

    /// <inheritdoc />
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        Logger.LogInformation("正在为文档 '{ContextDocumentName}' 应用 SignalR DTO 注入。", context.DocumentName);

        var dtoTypes = ApplicationModules.GetAllModules<IProgramModuleSignalRTypeProvider>()
            .SelectMany(module => module.GetSignalRDtoTypes(context))
            .Distinct().ToList();
        if (!dtoTypes.Any()) return;
        
        Logger.LogInformation("找到 {DtoTypesCount} 个 SignalR DTO 类型需要确保在 Schema 中:", dtoTypes.Count);
        foreach (var dtoType in dtoTypes)
        {
            Logger.LogInformation("- {DtoTypeFullName}", dtoType.FullName);
            // 使用 SchemaGenerator 确保该类型及其所有依赖的 Schema 被生成并添加到 SchemaRepository 中
            // GenerateSchema 方法会处理递归引用，并将 Schema 添加到 context.SchemaRepository.Schemas
            // 如果 Schema 已存在，它不会重复添加。
            // 我们不需要手动将返回的 Schema 添加到 swaggerDoc.Components.Schemas，
            // Swashbuckle 会在最后根据 SchemaRepository 填充 Components.Schemas。
            this._schemaGenerator.GenerateSchema(dtoType, context.SchemaRepository);
        }

        Logger.LogInformation("SignalR DTO Schema 生成检查完成。");
    }
}