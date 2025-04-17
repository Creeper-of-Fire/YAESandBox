// --- START OF FILE YAESandBox.Tests/API/Controllers/PersistenceControllerTests.cs ---

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using YAESandBox.API.Controllers;
using YAESandBox.Core.Block; // For BlockManager

namespace YAESandBox.Tests.API.Controllers;

public class PersistenceControllerTests
{
    private readonly Mock<IBlockManager> _mockBlockManager; // Mock BlockManager (需要它是可 mock 的，或者 mock IBlockManager)
                                                           // 注意：BlockManager 本身可能不易 mock，最好依赖 IBlockManager 接口
                                                           // 这里假设我们可以 mock BlockManager 或其接口
    private readonly PersistenceController _controller;

    public PersistenceControllerTests()
    {
        // 如果 BlockManager 有无参数构造函数或者可以轻松模拟其依赖项：
        this._mockBlockManager = new Mock<IBlockManager>();

        this._controller = new PersistenceController(this._mockBlockManager.Object);
    }

    [Fact]
    public async Task SaveState_当保存成功时_应返回FileStreamResult()
    {
        // Arrange
        var blindData = new { frontend = "data" };
        // 设置 SaveToFileAsync 行为：它应该向传入的 Stream 写入数据
        this._mockBlockManager.Setup(bm => bm.SaveToFileAsync(It.IsAny<Stream>(), blindData))
            .Callback<Stream, object?>((stream, data) =>
            {
                // 模拟写入 JSON 数据到流
                using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
                writer.Write("{\"status\":\"saved\"}");
                writer.Flush(); // 确保写入
            })
            .Returns(Task.CompletedTask); // 返回完成的任务

        // Act
        var result = await this._controller.SaveState(blindData);

        // Assert
        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/json");
        fileResult.FileDownloadName.Should().StartWith("yaesandbox_save_").And.EndWith(".json");

        // 检查流内容（可选，但更彻底）
        using var reader = new StreamReader(fileResult.FileStream);
        var content = await reader.ReadToEndAsync();
        content.Should().Be("{\"status\":\"saved\"}");

        this._mockBlockManager.Verify(bm => bm.SaveToFileAsync(It.IsAny<Stream>(), blindData), Times.Once);
    }

    [Fact]
    public async Task SaveState_当保存失败时_应返回InternalServerError()
    {
        // Arrange
        var blindData = new { data = 1 };
        this._mockBlockManager.Setup(bm => bm.SaveToFileAsync(It.IsAny<Stream>(), blindData))
            .ThrowsAsync(new Exception("模拟保存失败")); // 模拟服务抛出异常

        // Act
        var result = await this._controller.SaveState(blindData);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        objectResult.Value.Should().Be("Failed to save state.");
        this._mockBlockManager.Verify(bm => bm.SaveToFileAsync(It.IsAny<Stream>(), blindData), Times.Once);
    }

    [Fact]
    public async Task LoadState_当加载成功时_应返回包含盲存数据的OkResult()
    {
        // Arrange
        var expectedBlindData = new { restored = true };
        var fileContent = "{\"some\":\"data\"}"; // 文件内容不直接重要，重要的是 LoadFromFileAsync 的返回值
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("archive.json");
        mockFile.Setup(f => f.Length).Returns(stream.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream); // 返回包含内容的流
        mockFile.Setup(f => f.ContentType).Returns("application/json");

        // 模拟 LoadFromFileAsync 返回盲存数据
        this._mockBlockManager.Setup(bm => bm.LoadFromFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedBlindData);

        // Act
        var result = await this._controller.LoadState(mockFile.Object);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedBlindData); // 验证返回的盲存数据

        this._mockBlockManager.Verify(bm => bm.LoadFromFileAsync(It.IsAny<Stream>()), Times.Once); // 验证传入了流
    }

    [Fact]
    public async Task LoadState_当未上传文件时_应返回BadRequest()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0); // 文件长度为 0
        mockFile.Setup(f => f.FileName).Returns("empty.json");

        // Act
        var result = await this._controller.LoadState(mockFile.Object);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("No archive file uploaded.");
        this._mockBlockManager.Verify(bm => bm.LoadFromFileAsync(It.IsAny<Stream>()), Times.Never); // 确认未调用加载
    }

    [Fact]
    public async Task LoadState_当文件格式无效时_应返回BadRequest()
    {
        // Arrange
        var fileContent = "不是有效的 JSON";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("invalid.json");
        mockFile.Setup(f => f.Length).Returns(stream.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        // 模拟 LoadFromFileAsync 因 JSON 错误抛出异常
        this._mockBlockManager.Setup(bm => bm.LoadFromFileAsync(It.IsAny<Stream>()))
            .ThrowsAsync(new JsonException("模拟 JSON 解析错误"));

        // Act
        var result = await this._controller.LoadState(mockFile.Object);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Invalid archive file format.");
        this._mockBlockManager.Verify(bm => bm.LoadFromFileAsync(It.IsAny<Stream>()), Times.Once);
    }

    [Fact]
    public async Task LoadState_当加载时发生其他错误时_应返回InternalServerError()
    {
        // Arrange
        var fileContent = "{\"valid\":\"json\"}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("error.json");
        mockFile.Setup(f => f.Length).Returns(stream.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        // 模拟 LoadFromFileAsync 抛出通用异常
        this._mockBlockManager.Setup(bm => bm.LoadFromFileAsync(It.IsAny<Stream>()))
            .ThrowsAsync(new Exception("模拟加载时内部错误"));

        // Act
        var result = await this._controller.LoadState(mockFile.Object);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        objectResult.Value.Should().Be("Failed to load state.");
        this._mockBlockManager.Verify(bm => bm.LoadFromFileAsync(It.IsAny<Stream>()), Times.Once);
    }

    [Fact]
    public async Task LoadState_当上传非Json文件时_应仍然尝试加载并返回结果() // 根据代码逻辑，非json扩展名仅记录警告
    {
        // Arrange
        var expectedBlindData = new { loaded = "anyway" };
        var fileContent = "{\"content\":\"data\"}"; // 假设内容是有效的 JSON
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("archive.txt"); // 非 .json 扩展名
        mockFile.Setup(f => f.Length).Returns(stream.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        this._mockBlockManager.Setup(bm => bm.LoadFromFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(expectedBlindData); // 假设加载成功

        // Act
        var result = await this._controller.LoadState(mockFile.Object);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedBlindData); // 仍然成功加载

        this._mockBlockManager.Verify(bm => bm.LoadFromFileAsync(It.IsAny<Stream>()), Times.Once);
    }
}
// --- END OF FILE YAESandBox.Tests/API/Controllers/PersistenceControllerTests.cs ---