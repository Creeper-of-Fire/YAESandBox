using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Workflow.Analysis;
using YAESandBox.Workflow.API.Controller;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.API;

/// <summary>
/// 注册模块到 Program.cs
/// </summary>
public class WorkflowConfigModule : IProgramModule, IProgramModuleSwaggerUiOptionsConfigurator, IProgramModuleMvcConfigurator,
    IProgramModuleAppConfigurator
{
    /// <summary>
    /// Api文档的GroupName
    /// </summary>
    internal const string WorkflowConfigGroupName = "v1-workflow-config";


    /// <inheritdoc />
    public void ConfigureSwaggerUi(SwaggerUIOptions options)
    {
        // 端点: AI API
        options.SwaggerEndpoint($"/swagger/{WorkflowConfigGroupName}/swagger.json", "YAESandBox API (Workflow Config)");
    }

    /// <inheritdoc />
    public void ConfigureMvc(IMvcBuilder mvcBuilder)
    {
        mvcBuilder.AddApplicationPart(typeof(ModuleConfigController).Assembly);
    }

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service)
    {
        service.AddSwaggerGen(options =>
        {
            // --- 定义 AiService 模块 API 文档 ---
            options.SwaggerDoc(WorkflowConfigGroupName, new OpenApiInfo
            {
                Title = "YAESandBox API (Workflow Config)",
                Version = "v1",
                Description = "包含工作流配置相关的API。"
            });

            options.AddSwaggerDocumentation(typeof(ModuleConfigController).Assembly);
        });

        service.AddSingleton<WorkflowConfigFileService>();
        service.AddTransient<WorkflowValidationService>();
    }

    // TODO: [PluginManagement] 考虑在多插件场景下，处理前端组件命名冲突问题。
    //  - 方案1: 强制约定插件组件名称需以插件名作为前缀。
    //  - 方案2: 后端在DiscoverDynamicAssets时，扫描所有插件声明的组件名，
    //           若有冲突，则抛出错误或自动重命名Schema中的x-vue-component/x-web-component指令值。
    //  - 方案3: 前端插件加载器对不同插件的同名组件进行命名空间隔离。
    //  目前，假定所有插件组件名称在全局范围内是唯一的。

    /// <inheritdoc />
    public void ConfigureApp(IApplicationBuilder app)
    {
        // 在这里实现插件静态文件的动态挂载逻辑

        // 从 IApplicationBuilder 的服务提供程序中获取 IWebHostEnvironment
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

        // 1. 定义插件的根目录
        string pluginsRootDirectory = Path.Combine(env.ContentRootPath, "Plugins");

        // 2. 确保插件根目录存在
        if (!Directory.Exists(pluginsRootDirectory))
        {
            return; // 没有插件目录，直接返回
        }

        // 3. 遍历所有插件文件夹
        string[] pluginDirectories = Directory.GetDirectories(pluginsRootDirectory);

        foreach (string pluginDir in pluginDirectories)
        {
            // 4. 检查每个插件是否有自己的 wwwroot 文件夹
            string pluginWwwRoot = Path.Combine(pluginDir, "wwwroot");
            if (!Directory.Exists(pluginWwwRoot)) continue;

            // 5. 获取插件名作为 URL 的一部分
            string pluginName = new DirectoryInfo(pluginDir).Name;

            // 6. 为这个插件注册专门的静态文件服务
            // 这会为每个插件创建一个独立的中间件实例
            app.UseStaticFiles(new StaticFileOptions
            {
                // URL 请求路径：所有对 /plugins/{pluginName}/* 的请求...
                RequestPath = $"/plugins/{pluginName}",

                // ...都将从这个插件的 wwwroot 文件夹中提供文件。
                FileProvider = new PhysicalFileProvider(pluginWwwRoot)
            });

            // (可选) 在开发环境中打印日志，确认挂载成功
            if (env.IsDevelopment())
            {
                Console.WriteLine($"[WorkflowConfigModule] 插件 '{pluginName}' 的静态文件已挂载: '{pluginWwwRoot}' -> '/plugins/{pluginName}'");
            }
        }
    }
}