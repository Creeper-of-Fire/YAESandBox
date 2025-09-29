using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Workflow.Core.Abstractions;
using YAESandBox.Workflow.Test.API.Controller;
using YAESandBox.Workflow.Test.API.GameHub;

namespace YAESandBox.Workflow.Test.API;

/// <summary>
/// 用于测试工作流执行功能的模块。
/// </summary>
public class WorkflowTestModule : IProgramModuleSwaggerUiOptionsConfigurator, IProgramModuleMvcConfigurator, IProgramModuleHubRegistrar
{
    /// <summary>
    /// Api文档的GroupName
    /// </summary>
    internal const string WorkflowTestGroupName = "v1-workflow-test";

    /// <inheritdoc />
    public void ConfigureSwaggerUi(SwaggerUIOptions options)
    {
        options.SwaggerEndpoint($"/swagger/{WorkflowTestGroupName}/swagger.json", "YAESandBox API (Workflow Test)");
    }

    /// <inheritdoc />
    public void ConfigureMvc(IMvcBuilder mvcBuilder)
    {
        mvcBuilder.AddApplicationPart(typeof(WorkflowExecutionController).Assembly);
    }

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service)
    {
        service.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(WorkflowTestGroupName, new OpenApiInfo
            {
                Title = "YAESandBox API (Workflow Test)",
                Version = "v1",
                Description = "提供工作流执行和测试相关API。"
            });

            options.AddSwaggerDocumentation(typeof(WorkflowExecutionController).Assembly);
        });

        // 注册模拟的 IWorkflowDataAccess
        service.AddSingleton<IWorkflowDataAccess, MockWorkflowDataAccess>();
    }

    /// <inheritdoc />
    public void MapHubs(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<WorkflowHub>("/hubs/game-era-test");
    }
}