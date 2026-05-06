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

public sealed class LoadContextHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_FileExists_ReturnsOkWithCharCount()
    {
        var yaml = "meta:\n  project: test\nstate:\n  done: {}";
        var reader = NewReaderWithMeta(yaml);
        var sut = MakeHandler(reader.Object);

        var context = CreateContext();
        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Loaded project context");
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
        var yaml = "meta:\n  project: test";
        var reader = NewReaderWithMeta(yaml);
        var sut = MakeHandler(reader.Object);

        var context = CreateContext();
        await sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<string>(ContextKeys.ProjectContext, out var stored).Should().BeTrue();
        stored.Should().Be(yaml);
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_DoesNotSetPipeline()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        var sut = MakeHandler(reader.Object);

        var context = CreateContext();
        await sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<string>(ContextKeys.ProjectContext, out _).Should().BeFalse();
    }

    private static Mock<ISandboxFileReader> NewReaderWithMeta(string contextYaml)
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync("/work", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "/work/.agentsmith" });
        reader.Setup(r => r.TryReadAsync("/work/.agentsmith/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextYaml);
        return reader;
    }

    private static LoadContextHandler MakeHandler(ISandboxFileReader reader)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader);
        return new LoadContextHandler(
            new ProjectMetaResolver(), factory.Object, NullLogger<LoadContextHandler>.Instance);
    }

    private LoadContextContext CreateContext()
    {
        var repo = new Repository(new BranchName("feature/test"), "https://github.com/test/test");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        return new LoadContextContext(repo, pipeline);
    }
}
