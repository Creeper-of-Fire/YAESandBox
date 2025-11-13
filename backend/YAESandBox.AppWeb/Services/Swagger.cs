using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using YAESandBox.Depend.Logger;
using YAESandBox.ModuleSystem.AspNet.Interface;

namespace YAESandBox.AppWeb.Services;

/// <summary>
/// 一个 Swagger 文档过滤器，用于确保某些类型（如 SignalR DTOs）被包含在 OpenAPI Schema 中，
/// 即使它们没有被任何 HTTP API Controller 直接引用。
/// 这对于依赖 Swagger Schema 生成 TypeScript 类型的前端代码生成器很有用。
/// </summary>
internal class AdditionalSchemaDocumentFilter(ISchemaGenerator schemaGenerator) : IDocumentFilter
{
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger<AdditionalSchemaDocumentFilter>();
    private readonly ISchemaGenerator _schemaGenerator = schemaGenerator ?? throw new ArgumentNullException(nameof(schemaGenerator));

    /// <inheritdoc />
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        Logger.Info("正在为文档 '{ContextDocumentName}' 应用额外 Schema 类型注入。", context.DocumentName);

        var typesToAdd = ApplicationModules.GetAllModules<IProgramModuleAdditionalSchemaProvider>()
            .SelectMany(module => module.GetAdditionalSchemaTypes(context))
            .Distinct().ToList();
        if (!typesToAdd.Any()) return;

        Logger.Info("找到 {TypesCount} 个额外类型需要确保在 Schema 中:", typesToAdd.Count);
        foreach (var type in typesToAdd)
        {
            Logger.Info("- {TypeFullName}", type.FullName);
            // 使用 SchemaGenerator 确保该类型及其所有依赖的 Schema 被生成并添加到 SchemaRepository 中
            // GenerateSchema 方法会处理递归引用，并将 Schema 添加到 context.SchemaRepository.Schemas
            // 如果 Schema 已存在，它不会重复添加。
            // 我们不需要手动将返回的 Schema 添加到 swaggerDoc.Components.Schemas，
            // Swashbuckle 会在最后根据 SchemaRepository 填充 Components.Schemas。
            this._schemaGenerator.GenerateSchema(type, context.SchemaRepository);
        }

        Logger.Info("额外 Schema 类型生成检查完成。");
    }
}