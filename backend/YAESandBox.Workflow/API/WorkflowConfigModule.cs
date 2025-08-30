using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Workflow.API.Controller;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.Core.Analysis;
using YAESandBox.Workflow.Rune.ExactRune;
using YAESandBox.Workflow.Rune.SillyTavern;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.API;

/// <summary>
/// 注册模块到 Program.cs
/// </summary>
public class WorkflowConfigModule :
    IProgramModuleSwaggerUiOptionsConfigurator, IProgramModuleMvcConfigurator, IProgramModuleWithInitialization, IProgramModuleRuneProvider
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
        mvcBuilder.AddApplicationPart(typeof(RuneConfigController).Assembly);
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

            options.AddSwaggerDocumentation(typeof(RuneConfigController).Assembly);
        });

        service.AddSingleton<WorkflowConfigFileService>();
        service.AddTransient<WorkflowValidationService>();
        service.AddTransient<TuumAnalysisService>();
        service.AddTransient<RuneAnalysisService>();
    }

    /// <inheritdoc />
    public void Initialize(ModuleInitializationContext context)
    {
        var runeProviders = context.AllModules.OfType<IProgramModuleRuneProvider>().ToList();
        RuneConfigTypeResolver.Initialize(runeProviders);
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> RuneConfigTypes { get; } =
    [
        typeof(EmitEventRuneConfig),
        typeof(PromptGenerationRuneConfig),
        typeof(AiRuneConfig),
        typeof(HistoryAppendRuneConfig),
        typeof(StaticVariableRuneConfig),
        typeof(TuumRuneConfig),
        typeof(SillyTavernRuneConfig)
    ];
}