using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.ModuleSystem.Abstractions;

namespace YAESandBox.ModuleSystem.AspNet.Interface;

/// <summary>
/// 实现此接口的模块可以配置Swagger UI选项
/// 用于自定义API文档界面的行为和外观
/// </summary>
public interface IProgramModuleSwaggerUiOptionsConfigurator : IProgramModule
{
    /// <summary>
    /// 配置Swagger UI选项
    /// </summary>
    /// <param name="options">注入的Swagger UI配置选项</param>
    void ConfigureSwaggerUi(SwaggerUIOptions options);
}