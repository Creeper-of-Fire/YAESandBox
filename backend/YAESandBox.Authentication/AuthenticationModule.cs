using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.Depend.AspNetCore;

// 使用你新的命名空间

namespace YAESandBox.Authentication;

/// <summary>
/// 注册认证和授权相关的服务到 Program.cs
/// </summary>
public class AuthenticationModule : IProgramModuleMvcConfigurator,IProgramModuleSwaggerUiOptionsConfigurator
{
    internal const string AuthenticationGroupName = "v1-authentication";

    /// <inheritdoc />
    public void ConfigureMvc(IMvcBuilder mvcBuilder)
    {
        // 注册 AuthController
        mvcBuilder.AddApplicationPart(typeof(AuthController).Assembly);
    }

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services)
    {
        var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();

        // --- 1. 注册认证服务和JWT配置 ---
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key 未配置")))
                };
            });

        // --- 2. 注册自定义的用户和密码服务 ---
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<UserService>();

        // --- 3. 让 Swagger UI 支持 JWT ---
        // 注意：这里只配置Swagger的 "SecurityDefinition", 具体的端点文档在各自模块中定义
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(AuthenticationGroupName, new OpenApiInfo
            {
                Title = "YAESandBox API (Authentication)",
                Version = "v1",
                Description = "认证相关的端点。"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT 授权标头，格式为：'Bearer {token}'",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
    }

    /// <inheritdoc />
    public void ConfigureSwaggerUi(SwaggerUIOptions options)
    {
        options.SwaggerEndpoint($"/swagger/{AuthenticationGroupName}/swagger.json", "YAESandBox API (Authentication)");
    }
}