using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public class LoadCodingPrinciplesHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_FileExists_LoadsContent()
    {
        var relativePath = ".agentsmith/coding-principles.md";
        var fullPath = "/work/.agentsmith/coding-principles.md";

        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(fullPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        reader.Setup(r => r.ReadRequiredAsync(fullPath, It.IsAny<CancellationToken>())).ReturnsAsync("# Test Principles");

        var handler = MakeHandler(reader.Object);
        var repo = new Repository(new BranchName("main"), "https://example.com");
        var pipeline = MakePipeline();
        var context = new LoadCodingPrinciplesContext(relativePath, repo, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.DomainRules).Should().Be("# Test Principles");
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsOkSoftFail()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = MakeHandler(reader.Object);
        var repo = new Repository(new BranchName("main"), "https://example.com");
        var pipeline = MakePipeline();
        var context = new LoadCodingPrinciplesContext("nonexistent/path.md", repo, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<string>(ContextKeys.DomainRules, out _).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_DefaultPathInContextsSubdir_ResolvesViaDiscovery()
    {
        var defaultPath = "/work/.agentsmith/coding-principles.md";
        var nestedFile = "/work/.agentsmith/contexts/default/coding-principles.md";

        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(defaultPath, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        reader.Setup(r => r.ExistsAsync(nestedFile, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        reader.Setup(r => r.ReadRequiredAsync(nestedFile, It.IsAny<CancellationToken>())).ReturnsAsync("# Sub Rules");

        var handler = MakeHandler(reader.Object);
        var repo = new Repository(new BranchName("main"), "https://example.com");
        var pipeline = MakePipeline();
        var context = new LoadCodingPrinciplesContext(".agentsmith/coding-principles.md", repo, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.DomainRules).Should().Be("# Sub Rules");
    }

    [Fact]
    public async Task ExecuteAsync_ContentAccessibleViaCodingPrinciplesAlias()
    {
        var relativePath = ".agentsmith/coding-principles.md";
        var fullPath = "/work/.agentsmith/coding-principles.md";

        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(fullPath, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        reader.Setup(r => r.ReadRequiredAsync(fullPath, It.IsAny<CancellationToken>())).ReturnsAsync("# Rules");

        var handler = MakeHandler(reader.Object);
        var repo = new Repository(new BranchName("main"), "https://example.com");
        var pipeline = MakePipeline();
        var context = new LoadCodingPrinciplesContext(relativePath, repo, pipeline);

        await handler.ExecuteAsync(context, CancellationToken.None);

        pipeline.Get<string>(ContextKeys.CodingPrinciples).Should().Be("# Rules");
    }

    private static LoadCodingPrinciplesHandler MakeHandler(ISandboxFileReader reader)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader);
        return new LoadCodingPrinciplesHandler(
            factory.Object,
            NullLogger<LoadCodingPrinciplesHandler>.Instance);
    }

    private static PipelineContext MakePipeline()
    {
        var pipeline = new PipelineContext();
        var sandbox = Mock.Of<ISandbox>();
        pipeline.Set(ContextKeys.Sandbox, sandbox);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["default"] = sandbox });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal)
            {
                ["default"] = new RemoteContextDiscovery("default", ".", "csharp")
            });
        return pipeline;
    }
}
