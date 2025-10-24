using Swashbuckle.AspNetCore.SwaggerGen;
using YAESandBox.ModuleSystem.Abstractions;

namespace YAESandBox.ModuleSystem.AspNet.Interface;

/// <summary>
/// 实现此接口的模块可以提供额外的类型，以确保它们被包含在 OpenAPI Schema 中，
/// 即使它们没有被任何 HTTP API Controller 直接引用。
/// 这对于例如 SignalR DTOs, 共享的基类, 或其他需要通过代码生成器暴露给前端的类型非常有用。
/// </summary>
public interface IProgramModuleAdditionalSchemaProvider : IProgramModule
{
    /// <summary>
    /// 获取需要包含在 OpenAPI Schema 中的额外类型列表。
    /// </summary>
    /// <param name="context">Swagger文档过滤器上下文，可用于根据不同的文档提供不同的类型。</param>
    /// <returns>需要被包含的类型集合。</returns>
    IEnumerable<Type> GetAdditionalSchemaTypes(DocumentFilterContext context);
}