using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;
using YAESandBox.Depend.Logger;

namespace YAESandBox.ModuleSystem.AspNet;



/// <summary>
/// Swagger帮助类，提供Swagger文档相关的辅助方法
/// </summary>
public static class SwaggerHelper
{
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger(nameof(SwaggerHelper));

    /// <summary>
    /// 为Swagger添加XML注释文档
    /// </summary>
    /// <param name="options">Swagger生成选项</param>
    /// <param name="assembly">需要添加注释的程序集</param>
    public static void AddSwaggerDocumentation(this SwaggerGenOptions options, Assembly assembly)
    {
        try
        {
            string xmlFilename = $"{assembly.GetName().Name}.xml";
            string xmlFilePath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlFilePath))
            {
                options.IncludeXmlComments(xmlFilePath);
                Logger.Info("加载 XML 注释: {XmlFilePath}", xmlFilePath);
            }
            else
            {
                Logger.Warn("警告: 未找到 XML 注释文件: {XmlFilePath}", xmlFilePath);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "加载 Contracts XML 注释时出错。");
        }
    }
}