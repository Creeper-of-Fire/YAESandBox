using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.PlayerServices.Save.SaveData;
using YAESandBox.PlayerServices.Save.SaveSlot;

namespace YAESandBox.PlayerServices.Save;

/// <summary>
/// 用于存储玩家存档的模块
/// </summary>
public class PlayerSaveModule : IProgramModuleSwaggerUiOptionsConfigurator, IProgramModuleMvcConfigurator
{
    /// <summary>
    /// Api文档的GroupName
    /// </summary>
    internal const string ProjectSaveSlotGroupName = "v1-player-save";

    /// <inheritdoc />
    public void ConfigureSwaggerUi(SwaggerUIOptions options)
    {
        options.SwaggerEndpoint($"/swagger/{ProjectSaveSlotGroupName}/swagger.json", "YAESandBox API (Player Save)");
    }

    /// <inheritdoc />
    public void ConfigureMvc(IMvcBuilder mvcBuilder)
    {
        mvcBuilder.AddApplicationPart(typeof(ProjectSaveSlotController).Assembly);
    }

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service)
    {
        service.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(ProjectSaveSlotGroupName, new OpenApiInfo
            {
                Title = "YAESandBox API (Player Save)",
                Version = "v1",
                Description = "为不同的项目提供游戏存档服务。"
            });

            options.AddSwaggerDocumentation(typeof(ProjectSaveSlotController).Assembly);
        });
        
        service.AddSingleton<UserSaveDataService>();
        service.AddSingleton<ProjectSaveSlotService>();
    }
}