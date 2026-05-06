using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class LoadCodeMapHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_FileExists_LoadsContent()
    {
        var yaml = "modules:\n  - name: Core\n    path: src/Core";
        var reader = NewReaderWithMeta(yaml);
        var sut = MakeHandler(reader.Object);

        var context = CreateContext();
        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Loaded code map");
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsOk()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        var sut = MakeHandler(reader.Object);

        var context = CreateContext();
        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_FileExists_StoresInPipeline()
    {
        var yaml = "modules:\n  - name: Core";
        var reader = NewReaderWithMeta(yaml);
        var sut = MakeHandler(reader.Object);

        var context = CreateContext();
        await sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<string>(ContextKeys.CodeMap, out var stored).Should().BeTrue();
        stored.Should().Be(yaml);
    }

    private static Mock<ISandboxFileReader> NewReaderWithMeta(string codeMapYaml)
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync("/work", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "/work/.agentsmith" });
        reader.Setup(r => r.TryReadAsync("/work/.agentsmith/code-map.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(codeMapYaml);
        return reader;
    }

    private static LoadCodeMapHandler MakeHandler(ISandboxFileReader reader)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader);
        return new LoadCodeMapHandler(
            new ProjectMetaResolver(), factory.Object, NullLogger<LoadCodeMapHandler>.Instance);
    }

    private LoadCodeMapContext CreateContext()
    {
        var repo = new Repository(new BranchName("feature/test"), "https://github.com/test/test");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        return new LoadCodeMapContext(repo, pipeline);
    }
}
