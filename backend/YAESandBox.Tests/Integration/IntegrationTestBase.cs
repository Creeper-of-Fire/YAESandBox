// --- START OF FILE IntegrationTestBase.cs ---

// For ReadFromJsonAsync
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
// Add your DTO namespaces
using YAESandBox.Core; // For TypedIdConverter if needed in client serialization

namespace YAESandBox.Tests.Integration;

/// <summary>
/// 集成测试的基类，提供 WebApplicationFactory 和 HttpClient。
/// 使用 IAsyncLifetime 确保每个测试类共享同一个应用实例，并在测试前后进行设置和清理。
/// 注意：如果测试间需要完全隔离，可能需要调整为每个测试方法创建新实例，但这会显著增加测试时间。
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    internal readonly WebApplicationFactory<Program> Factory;
    protected HttpClient HttpClient { get; private set; } = null!;

    // 用于 SignalR 消息序列化的选项，应与服务器端配置一致
    internal static readonly JsonSerializerOptions SignalRJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(), new TypedIdConverter() }, // 确保包含所有必要的转换器
        PropertyNameCaseInsensitive = true // 如果需要
    };

    protected IntegrationTestBase()
    {
        // 配置 WebApplicationFactory
        this.Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // 可在此处配置测试特定的服务或设置
                // builder.ConfigureServices(services => { ... });
                // builder.UseEnvironment("Testing"); // 如果有测试环境特定配置
            });
    }

    /// <summary>
    /// 在测试类开始时执行一次。
    /// </summary>
    public virtual Task InitializeAsync()
    {
        this.HttpClient = this.Factory.CreateClient();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 在测试类结束时执行一次。
    /// </summary>
    public virtual Task DisposeAsync()
    {
        this.HttpClient?.Dispose();
        this.Factory?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 创建并配置连接到 GameHub 的 SignalR HubConnection。
    /// </summary>
    /// <returns>一个配置好的 HubConnection 实例。</returns>
    protected HubConnection CreateHubConnection()
    {
        // 从 Factory 获取 SignalR 服务器的 URL
        // WebApplicationFactory 会自动选择一个可用端口
        // 需要一种方式获取测试服务器的基地址，包括端口
        // 一种常见方法是通过 Factory.Server.BaseAddress，但这可能需要服务器启动后才能获取
        // 或者直接使用 HttpClient 的 BaseAddress (如果配置了)
        // 更可靠的方式是注入 TestServer 并从中获取 Handler

        // 这里使用一种简化的方式，直接构建 URL，依赖于 Kestrel 的默认行为或配置
        // 注意：这可能不够健壮，实际项目中可能需要更可靠的端口获取方式
        var serverHandler = this.Factory.Server.CreateHandler(); // 获取内部 TestServer 的 Handler

        var connection = new HubConnectionBuilder()
            .WithUrl("ws://localhost/gamehub", options => // 使用 ws:// 避免 HTTPS 证书问题
            {
                // 关键：将 HttpClient 的 Handler 传递给 SignalR Client
                // 这样 SignalR Client 就可以直接与内存中的 TestServer 通信
                options.HttpMessageHandlerFactory = _ => serverHandler;
            })
            .AddJsonProtocol(options => // 确保使用与服务器相同的 JSON 配置
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                // 添加其他必要的转换器，例如 TypedIdConverter
                 options.PayloadSerializerOptions.Converters.Add(new TypedIdConverter());
            })
            .Build();

        return connection;
    }

     /// <summary>
    /// 辅助方法：等待 SignalR 连接成功建立。
    /// </summary>
    protected static async Task ConnectHubAsync(HubConnection connection, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10)); // 设置连接超时
        try
        {
            await connection.StartAsync(cts.Token);
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException("连接到 SignalR Hub 超时。", ex);
        }
    }

    /// <summary>
    /// 辅助方法：安全地停止 SignalR 连接。
    /// </summary>
    protected static async Task StopHubAsync(HubConnection? connection)
    {
        if (connection != null && connection.State != HubConnectionState.Disconnected)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // 设置停止超时
            try
            {
                await connection.StopAsync(cts.Token);
            }
            catch (Exception ex)
            {
                // 记录停止时发生的错误，但不抛出，避免影响测试清理
                Console.WriteLine($"停止 SignalR 连接时出错: {ex.Message}");
            }
            await connection.DisposeAsync();
        }
    }
}
// --- END OF FILE IntegrationTestBase.cs ---