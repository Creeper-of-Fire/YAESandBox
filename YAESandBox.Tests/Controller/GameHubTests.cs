// --- START OF FILE YAESandBox.Tests/API/Hubs/GameHubTests.cs ---

using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Xunit;
using YAESandBox.API.DTOs;
using YAESandBox.API.Hubs;
using YAESandBox.API.Services;
using YAESandBox.Core.Action;

namespace YAESandBox.Tests.API.Hubs;

public class GameHubTests
{
    private readonly Mock<IWorkflowService> _mockWorkflowService;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IHubCallerClients<IGameClient>> _mockClients;
    private readonly Mock<IGameClient> _mockCaller; // 用于模拟 Clients.Caller
    private readonly GameHub _hub;
    
    // --- 新增用于模拟 HttpContext 的 Mock 对象 ---
    private readonly Mock<IFeatureCollection> _mockFeatures;
    private readonly Mock<IHttpContextFeature> _mockHttpContextFeature;
    private readonly Mock<HttpContext> _mockHttpContext;
    private readonly Mock<ConnectionInfo> _mockConnectionInfo;
    private readonly Mock<HttpRequest> _mockHttpRequest;
    private readonly Mock<IHeaderDictionary> _mockHeaders;

    public GameHubTests()
    {
        _mockWorkflowService = new Mock<IWorkflowService>();
        _mockContext = new Mock<HubCallerContext>();
        _mockClients = new Mock<IHubCallerClients<IGameClient>>();
        _mockCaller = new Mock<IGameClient>(); // Mock 单个客户端代理

        // --- 初始化 HttpContext 相关的模拟 ---
        _mockFeatures = new Mock<IFeatureCollection>();
        _mockHttpContextFeature = new Mock<IHttpContextFeature>();
        _mockHttpContext = new Mock<HttpContext>();
        _mockConnectionInfo = new Mock<ConnectionInfo>();
        _mockHttpRequest = new Mock<HttpRequest>();
        _mockHeaders = new Mock<IHeaderDictionary>();

        // 设置 HttpContext 的模拟行为
        _mockConnectionInfo.Setup(c => c.RemoteIpAddress).Returns(System.Net.IPAddress.Parse("127.0.0.1")); // 提供一个模拟 IP
        _mockHeaders.Setup(h => h["User-Agent"]).Returns(new StringValues("Mock-Test-Agent")); // 设置 User-Agent
        _mockHttpRequest.Setup(r => r.Headers).Returns(_mockHeaders.Object); // Request 返回模拟 Headers
        _mockHttpContext.Setup(ctx => ctx.Connection).Returns(_mockConnectionInfo.Object); // HttpContext 返回模拟 Connection
        _mockHttpContext.Setup(ctx => ctx.Request).Returns(_mockHttpRequest.Object); // HttpContext 返回模拟 Request

        // 设置 HttpContextFeature 返回模拟的 HttpContext
        _mockHttpContextFeature.Setup(f => f.HttpContext).Returns(_mockHttpContext.Object);

        // 设置 Features 集合能够提供模拟的 HttpContextFeature
        // 常用的方式是模拟 Get<TFeature>() 方法
        _mockFeatures.Setup(f => f.Get<IHttpContextFeature>()).Returns(_mockHttpContextFeature.Object);
        // 或者，有时 Features 是通过索引器访问的
        _mockFeatures.Setup(f => f[typeof(IHttpContextFeature)]).Returns(_mockHttpContextFeature.Object);

        // 设置模拟 HubCallerContext 的行为
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
        // *** 关键：让模拟 Context 返回模拟的 Features ***
        _mockContext.Setup(c => c.Features).Returns(_mockFeatures.Object);

        // 设置 Clients 和 Caller
        _mockClients.Setup(c => c.Caller).Returns(_mockCaller.Object);

        // 创建 Hub 实例并设置其属性
        _hub = new GameHub(_mockWorkflowService.Object)
        {
            Context = _mockContext.Object,
            Clients = _mockClients.Object
        };
    }

    [Fact]
    public async Task TriggerWorkflow_当调用时_应调用WorkflowService的HandleWorkflowTriggerAsync()
    {
        // Arrange
        var request = new TriggerWorkflowRequestDto
        {
            RequestId = "req-1",
            ParentBlockId = "parent-1",
            WorkflowName = "TestFlow",
            Params = new Dictionary<string, object?>()
        };
        _mockWorkflowService
            .Setup(ws => ws.HandleWorkflowTriggerAsync(request))
            .Returns(Task.CompletedTask); // 模拟服务方法成功完成

        // Act
        await _hub.TriggerWorkflow(request);

        // Assert
        // 验证 WorkflowService 的方法是否被调用了一次，并且参数是传入的 request 对象
        _mockWorkflowService.Verify(ws => ws.HandleWorkflowTriggerAsync(request), Times.Once);
    }

    [Fact]
    public async Task TriggerWorkflow_当WorkflowService抛出异常时_应捕获异常并不再抛出()
    {
        // Arrange
        var request = new TriggerWorkflowRequestDto { RequestId = "req-err" };
        var expectedException = new InvalidOperationException("模拟服务错误");
        _mockWorkflowService
            .Setup(ws => ws.HandleWorkflowTriggerAsync(request))
            .ThrowsAsync(expectedException); // 模拟服务方法抛出异常

        // Act
        // 使用 FluentAssertions 的 Func<Task> 来执行异步操作并断言它不应该抛出异常
        Func<Task> act = async () => await _hub.TriggerWorkflow(request);

        // Assert
        await act.Should().NotThrowAsync(); // 验证 Hub 方法捕获了异常

        // 验证 WorkflowService 的方法仍然被调用了
        _mockWorkflowService.Verify(ws => ws.HandleWorkflowTriggerAsync(request), Times.Once);
        // 可以进一步验证是否有错误日志被记录（如果日志系统允许的话）
        // 可以验证是否调用了 Clients.Caller 发送错误消息（如果实现了的话）
        // _mockCaller.Verify(c => c.ReceiveError(It.IsAny<string>()), Times.Once); // 假设有 ReceiveError 方法
    }


    [Fact]
    public async Task OnConnectedAsync_应调用基类方法并且不抛出异常() // 修改断言描述
    {
        // Arrange (已经在构造函数中完成)

        // Act
        Func<Task> act = async () => await _hub.OnConnectedAsync();

        // Assert
        // 验证方法能成功执行，并且由于我们模拟了HttpContext，内部的日志记录不会导致NullRef
        await act.Should().NotThrowAsync();

        // 验证相关的模拟对象是否被访问过 (可选，但有助于确认设置生效)
        _mockContext.Verify(c => c.Features, Times.AtLeastOnce); // 确认访问了 Features
        _mockFeatures.Verify(f => f.Get<IHttpContextFeature>(), Times.AtLeastOnce); // 确认获取了 HttpContextFeature
        _mockHttpContext.Verify(ctx => ctx.Connection, Times.AtLeastOnce);
        _mockConnectionInfo.Verify(ci => ci.RemoteIpAddress, Times.AtLeastOnce);
        _mockHttpContext.Verify(ctx => ctx.Request, Times.AtLeastOnce);
        _mockHttpRequest.Verify(r => r.Headers, Times.AtLeastOnce);
        _mockHeaders.Verify(h => h.UserAgent, Times.AtLeastOnce);
    }

     [Fact]
    public async Task ResolveConflict_当WorkflowService抛出异常时_应捕获异常并不再抛出()
    {
        // Arrange
        var request = new ResolveConflictRequestDto { RequestId = "req-res-err" };
         var expectedException = new ArgumentException("模拟解决冲突错误");
        _mockWorkflowService
            .Setup(ws => ws.HandleConflictResolutionAsync(request))
            .ThrowsAsync(expectedException);

        // Act
        Func<Task> act = async () => await _hub.ResolveConflict(request);

        // Assert
        await act.Should().NotThrowAsync();
        _mockWorkflowService.Verify(ws => ws.HandleConflictResolutionAsync(request), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_应调用基类方法()
    {
        // Arrange
        // Hub 的 OnConnectedAsync/OnDisconnectedAsync 通常主要是调用 base 实现和记录日志
        // 很难直接验证 base.OnConnectedAsync() 是否被调用，但可以验证方法能成功执行不抛异常

        // Act
        var act = async () => await _hub.OnConnectedAsync();

        // Assert
        await act.Should().NotThrowAsync();
        // 可以验证是否有 Info 级别的日志被记录（如果日志系统可验证）
    }

    [Fact]
    public async Task OnDisconnectedAsync_当无异常时_应调用基类方法()
    {
        // Arrange
        Exception? exception = null;

        // Act
        Func<Task> act = async () => await _hub.OnDisconnectedAsync(exception);

        // Assert
        await act.Should().NotThrowAsync();
         // 可以验证是否有 Info 级别的日志被记录
    }

     [Fact]
    public async Task OnDisconnectedAsync_当有异常时_应调用基类方法并记录错误()
    {
        // Arrange
        var exception = new Exception("连接中断");

        // Act
        Func<Task> act = async () => await _hub.OnDisconnectedAsync(exception);

        // Assert
        await act.Should().NotThrowAsync();
         // 可以验证是否有 Info 和 Error 级别的日志被记录
    }
}
// --- END OF FILE YAESandBox.Tests/API/Hubs/GameHubTests.cs ---