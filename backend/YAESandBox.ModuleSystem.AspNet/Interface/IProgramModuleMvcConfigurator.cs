using Microsoft.Extensions.DependencyInjection;
using YAESandBox.ModuleSystem.Abstractions;

namespace YAESandBox.ModuleSystem.AspNet.Interface;

/// <summary>
/// 实现此接口的模块可以配置MVC相关设置
/// 用于自定义MVC框架的行为
/// </summary>
public interface IProgramModuleMvcConfigurator : IProgramModule
{
    /// <summary>
    /// 可以添加Mvc配置
    /// </summary>
    /// <param name="mvcBuilder"></param>
    void ConfigureMvc(IMvcBuilder mvcBuilder);
}