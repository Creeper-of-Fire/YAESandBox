using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using YAESandBox.API.DTOs.WebSocket;

namespace YAESandBox.API;

/// <summary>
/// 一个 Swagger 文档过滤器，用于确保 SignalR 使用的 DTOs 被包含在 OpenAPI Schema 中，
/// 即使它们没有被任何 HTTP API Controller 直接引用。
/// 这对于依赖 Swagger Schema 生成 TypeScript 类型的前端代码生成器很有用。
/// </summary>
public class SignalRDtoDocumentFilter(ISchemaGenerator schemaGenerator) : IDocumentFilter
{
    private readonly ISchemaGenerator _schemaGenerator = schemaGenerator ?? throw new ArgumentNullException(nameof(schemaGenerator));

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // 检查当前正在生成的文档是否是我们想要应用此 Filter 的目标文档
        // 例如，我们可能只希望在 "v1-internal"和"v-public" 文档中包含这些 SignalR DTOs，
        // 或者在一个专门的 "v1-signalr" 文档中。
        if (context.DocumentName is not (GlobalSwaggerConstants.PublicApiGroupName or GlobalSwaggerConstants.InternalApiGroupName))
        {
            // 如果不是目标文档，则不执行任何操作
            Console.WriteLine($"SignalRDtoDocumentFilter: 跳过文档 '{context.DocumentName}'，因为非目标文档。");
            return;
        }

        Console.WriteLine($"SignalRDtoDocumentFilter: 正在为文档 '{context.DocumentName}' 应用 SignalR DTO 注入。");

        // 定义包含 SignalR DTOs 的程序集和命名空间
        var targetAssembly = typeof(TriggerMainWorkflowRequestDto).Assembly; // 获取 DTO 所在的程序集
        string? targetNamespace = typeof(TriggerMainWorkflowRequestDto).Namespace; // 获取 DTO 所在的命名空间

        if (targetNamespace == null)
        {
            Console.WriteLine("警告: 无法确定 SignalR DTO 的目标命名空间。");
            return;
        }

        // 查找目标命名空间下所有公共的 record 和 class 类型
        var dtoTypes = targetAssembly.GetTypes()
            .Where(t => t.IsPublic && // 必须是公共类型
                        t.Namespace == targetNamespace && // 必须在目标命名空间下
                        !t.IsEnum && // 排除枚举（枚举如果被 DTO 引用，会被自动处理）
                        !t.IsInterface && // 排除接口
                        !t.IsAbstract && // 排除抽象类（除非需要）
                        (t.IsClass || (t.IsValueType && !t.IsPrimitive))) // 包括类和结构体（记录是类）
            .ToList();

        Console.WriteLine($"找到 {dtoTypes.Count} 个 SignalR DTO 类型需要确保在 Schema 中:");
        foreach (var dtoType in dtoTypes)
        {
            Console.WriteLine($"- {dtoType.FullName}");
            // 使用 SchemaGenerator 确保该类型及其所有依赖的 Schema 被生成并添加到 SchemaRepository 中
            // GenerateSchema 方法会处理递归引用，并将 Schema 添加到 context.SchemaRepository.Schemas
            // 如果 Schema 已存在，它不会重复添加。
            // 我们不需要手动将返回的 Schema 添加到 swaggerDoc.Components.Schemas，
            // Swashbuckle 会在最后根据 SchemaRepository 填充 Components.Schemas。
            this._schemaGenerator.GenerateSchema(dtoType, context.SchemaRepository);
        }

        Console.WriteLine("SignalR DTO Schema 生成检查完成。");

        // 注意：枚举类型（如 StreamStatus, UpdateMode）如果被上面的 DTO 引用，
        // GenerateSchema 调用会自动处理它们，不需要在这里单独添加。
        // 如果有独立的、未被引用的枚举需要添加，可以在这里类似地处理。
        var enumTypes = targetAssembly.GetTypes()
            .Where(t => t.IsPublic && t.Namespace == targetNamespace && t.IsEnum)
            .ToList();

        if (enumTypes.Any())
        {
            Console.WriteLine($"找到 {enumTypes.Count} 个 SignalR 枚举类型需要确保在 Schema 中:");
            foreach (var enumType in enumTypes)
            {
                // 同样，如果枚举尚未被引用，强制生成其 Schema
                Console.WriteLine($"- {enumType.FullName}");
                this._schemaGenerator.GenerateSchema(enumType, context.SchemaRepository);
            }

            Console.WriteLine("SignalR 枚举 Schema 生成检查完成。");
        }
    }
}