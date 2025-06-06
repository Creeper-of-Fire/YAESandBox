using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.Core.API.Hubs;
using YAESandBox.Core.API.Services;
using YAESandBox.Core.Block.BlockManager;
using YAESandBox.Core.DTOs;
using YAESandBox.Core.DTOs.WebSocket;
using YAESandBox.Core.Services;
using YAESandBox.Core.Services.InterFaceAndBasic;
using YAESandBox.Core.Services.WorkFlow;
using YAESandBox.Depend;
using YAESandBox.Depend.AspNetCore;

namespace YAESandBox.Core.API;

/// <summary>
/// TODO: 暂时的核心模块存放点，之后会重构为非核心模块
/// </summary>
public class CoreModule : IProgramModule, IProgramModuleSwaggerUiOptionsConfigurator, IProgramModuleMvcConfigurator,
    IProgramModuleHubRegistrar, IProgramModuleSignalRTypeProvider
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service)
    {
        service.AddSwaggerGen(options =>
        {
            // --- 定义公开 API 文档 ---
            options.SwaggerDoc(GlobalSwaggerConstants.PublicApiGroupName, new OpenApiInfo
            {
                Title = "YAESandBox API (Public)",
                Version = "v1",
                Description = "供前端和外部使用的公开 API 文档。"
            });

            // --- 定义内部/调试 API 文档 ---
            options.SwaggerDoc(GlobalSwaggerConstants.DebugApiGroupName, new OpenApiInfo
            {
                Title = "YAESandBox API (Debug)",
                Version = "v1",
                Description = "包含所有 API，包括内部调试接口。"
            });

            // --- 加载 XML 注释文件 ---

            // 1. 加载 API 项目自身的 XML 注释文件
            options.AddSwaggerDocumentation(Assembly.GetExecutingAssembly());

            // 2. 加载 Service 项目的 XML 注释文件
            options.AddSwaggerDocumentation(typeof(BasicBlockService).Assembly);

            // 3. 加载共享 DTO 项目的 XML 注释文件
            options.AddSwaggerDocumentation(typeof(AtomicOperationRequestDto).Assembly);

            options.AddSwaggerDocumentation(typeof(BlockResultCode).Assembly);
        });

        // NotifierService depends on IHubContext, BlockManager, usually Singleton
        service.AddSingleton<SignalRNotifierService>()
            .AddSingleton<INotifierService>(sp => sp.GetRequiredService<SignalRNotifierService>())
            .AddSingleton<IWorkflowNotifierService>(sp => sp.GetRequiredService<SignalRNotifierService>());

        // BlockManager holds state, make it Singleton.
        service.AddSingleton<IBlockManager, BlockManager>();

        // WorkflowService depends on IBlockManager and INotifierService, make it Singleton or Scoped.
        // Singleton is fine if it doesn't hold per-request state.
        service.AddSingleton<IWorkFlowBlockService, WorkFlowBlockService>();
        service.AddSingleton<IWorkflowService, WorkflowService>();
        service.AddSingleton<IBlockManagementService, BlockManagementService>();
        service.AddSingleton<IBlockWritService, BlockWritService>();
        service.AddSingleton<IBlockReadService, BlockReadService>();
    }

    /// <inheritdoc />
    public void ConfigureSwaggerUi(SwaggerUIOptions options)
    {
        // --- 配置 UI 以显示文档版本 ---
        // 端点 1: 公开 API
        options.SwaggerEndpoint($"/swagger/{GlobalSwaggerConstants.PublicApiGroupName}/swagger.json", $"YAESandBox API (Public)");
        // 端点 2: 内部 API
        options.SwaggerEndpoint($"/swagger/{GlobalSwaggerConstants.DebugApiGroupName}/swagger.json", $"YAESandBox API (Debug)");
    }

    /// <inheritdoc />
    public void ConfigureMvc(IMvcBuilder mvcBuilder)
    {
        mvcBuilder.AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    /// <inheritdoc />
    public void MapHubs(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<GameHub>("/gamehub");
    }

    /// <inheritdoc />
    public IEnumerable<Type> GetSignalRDtoTypes(DocumentFilterContext document)
    {
        if (document.DocumentName is not (GlobalSwaggerConstants.PublicApiGroupName or GlobalSwaggerConstants.DebugApiGroupName))
        {
            // 如果不是目标文档，则不执行任何操作
            Console.WriteLine($"SignalRDtoDocumentFilter: 跳过文档 '{document.DocumentName}'，因为非目标文档。");
            return [];
        }

        // 定义包含 SignalR DTOs 的程序集和命名空间
        var targetAssembly = typeof(TriggerMainWorkflowRequestDto).Assembly; // 获取 DTO 所在的程序集
        string? targetNamespace = typeof(TriggerMainWorkflowRequestDto).Namespace; // 获取 DTO 所在的命名空间

        if (targetNamespace == null)
        {
            Console.WriteLine("警告: 无法确定 SignalR DTO 的目标命名空间。");
            return [];
        }

        // 查找目标命名空间下所有公共的 record 和 class 类型
        var dtoTypes = targetAssembly.GetTypes()
            .Where(t => t.IsPublic && // 必须是公共类型
                        t.Namespace == targetNamespace && // 必须在目标命名空间下
                        !t.IsEnum && // 排除枚举（枚举如果被 DTO 引用，会被自动处理）
                        !t.IsInterface && // 排除接口
                        !t.IsAbstract && // 排除抽象类（除非需要）
                        (t.IsClass || t is { IsValueType: true, IsPrimitive: false })) // 包括类和结构体（记录是类）
            .ToList();
        return dtoTypes;
    }
}

internal static class GlobalSwaggerConstants
{
    // --- 定义文档名称常量 ---
    internal const string PublicApiGroupName = "v1-public"; // 公开 API 文档
    internal const string DebugApiGroupName = "v1-debug"; // 内部/调试 API 文档
}