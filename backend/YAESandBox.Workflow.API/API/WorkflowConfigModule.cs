using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.ModuleSystem.Abstractions;
using YAESandBox.ModuleSystem.AspNet;
using YAESandBox.Workflow.API.API.Controller;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.Runtime.WorkflowService;
using YAESandBox.Workflow.Core.Service;
using YAESandBox.Workflow.Core.VarSpec;
using YAESandBox.Workflow.ExactRune;
using YAESandBox.Workflow.ExactRune.SillyTavern;
using YAESandBox.Workflow.WorkflowService.Analysis;
using PromptGenerationRuneConfig = YAESandBox.Workflow.ExactRune.PromptGenerationRuneConfig;

namespace YAESandBox.Workflow.API.API;

/// <summary>
/// 注册模块到 Program.cs
/// </summary>
public class WorkflowConfigModule :
    IProgramModuleSwaggerUiOptionsConfigurator, IProgramModuleMvcConfigurator, IProgramModuleWithInitialization, IProgramModuleRuneProvider,
    IProgramModuleAdditionalSchemaProvider
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

        service.AddSingleton<WorkflowConfigFilePersistenceService>();
        service.AddSingleton<WorkflowConfigFindService>();
        service.AddTransient<WorkflowValidationService>();
        service.AddTransient<TuumAnalysisService>();
        service.AddTransient<RuneAnalysisService>();
    }

    /// <inheritdoc />
    public IEnumerable<Type> GetAdditionalSchemaTypes(DocumentFilterContext context)
    {
        bool isVarSpecDefReferenced = context.SchemaRepository.Schemas.ContainsKey(nameof(VarSpecDef));

        if (!isVarSpecDefReferenced) yield break;
        
        yield return typeof(PrimitiveVarSpecDef);
        yield return typeof(RecordVarSpecDef);
        yield return typeof(ListVarSpecDef);
    }

    /// <inheritdoc />
    public void Initialize(ModuleInitializationContext context)
    {
        var runeProviders = context.AllModules.OfType<IProgramModuleRuneProvider>().ToList();
        RuneConfigTypeResolver.Initialize(runeProviders);
        var innerConfigProviders = context.AllModules.OfType<IProgramModuleInnerConfigProvider>().ToList();
        InnerConfigProviderResolver.Initialize(innerConfigProviders);
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
        typeof(SillyTavernRuneConfig),
        typeof(UnknownRuneConfig),
        typeof(StringParserToValueRuneConfig),
        typeof(ConditionalPromptRuneConfig),
        typeof(TemplateParserRuneConfig),
        typeof(TextTemplateRuneConfig),
        typeof(ReferenceRuneConfig),
    ];
}