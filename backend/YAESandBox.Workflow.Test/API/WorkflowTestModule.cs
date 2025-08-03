namespace YAESandBox.Workflow.Test.API;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using Depend.AspNetCore;
using Abstractions;
using Controller;

/// <summary>
/// 用于测试工作流执行功能的模块。
/// </summary>
public class WorkflowTestModule : IProgramModule, IProgramModuleSwaggerUiOptionsConfigurator, IProgramModuleMvcConfigurator
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
}