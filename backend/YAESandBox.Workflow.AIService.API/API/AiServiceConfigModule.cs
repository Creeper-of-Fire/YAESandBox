using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.ModuleSystem.AspNet;
using YAESandBox.ModuleSystem.AspNet.Interface;
using YAESandBox.Workflow.AIService.AiConfig;
using YAESandBox.Workflow.AIService.API.API.Controller;
using YAESandBox.Workflow.AIService.ConfigManagement;

namespace YAESandBox.Workflow.AIService.API.API;

/// <summary>
/// 注册模块到 Program.cs
/// </summary>
public class AiServiceConfigModule : IProgramModuleSwaggerUiOptionsConfigurator, IProgramModuleMvcConfigurator
{
    /// <summary>
    /// Api文档的GroupName
    /// </summary>
    internal const string AiConfigGroupName = "v1-ai-config";


    /// <inheritdoc />
    public void ConfigureSwaggerUi(SwaggerUIOptions options)
    {
        // 端点: AI API
        options.SwaggerEndpoint($"/swagger/{AiConfigGroupName}/swagger.json", "YAESandBox API (AI Config)");
    }

    /// <inheritdoc />
    public void ConfigureMvc(IMvcBuilder mvcBuilder)
    {
        mvcBuilder.AddApplicationPart(typeof(AiConfigurationsController).Assembly);
    }

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service)
    {
        service.AddSwaggerGen(options =>
        {
            // --- 定义 AiService 模块 API 文档 ---
            options.SwaggerDoc(AiConfigGroupName, new OpenApiInfo
            {
                Title = "YAESandBox API (AI Config)",
                Version = "v1",
                Description = "包含AI服务配置相关的API。"
            });

            options.AddSwaggerDocumentation(typeof(AiConfigurationsController).Assembly);
            options.AddSwaggerDocumentation(typeof(AbstractAiProcessorConfig).Assembly);
        });

        service.AddSingleton<JsonFileAiConfigurationManager>()
            .AddSingleton<IAiConfigurationManager>(sp => sp.GetRequiredService<JsonFileAiConfigurationManager>())
            .AddSingleton<IAiConfigurationProvider>(sp => sp.GetRequiredService<JsonFileAiConfigurationManager>());
        
        service.AddSingleton<IAiHttpClientFactory, AspNetAiHttpClientFactory>();

        // 注册 MasterAiService。现在 DI 容器知道如何创建它了。
        // 它会自动获取上面注册的 IHttpClientFactory 和 IAiConfigurationManager。
        service.AddSingleton<IMasterAiService, MasterAiService>();
    }
}