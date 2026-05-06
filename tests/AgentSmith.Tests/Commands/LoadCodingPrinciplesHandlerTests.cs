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
        reader.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var handler = MakeHandler(reader.Object);
        var repo = new Repository(new BranchName("main"), "https://example.com");
        var pipeline = MakePipeline();
        var context = new LoadCodingPrinciplesContext("nonexistent/path.md", repo, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<string>(ContextKeys.DomainRules, out _).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_DefaultPathInMonorepoSubdir_ResolvesViaProjectMetaResolver()
    {
        var defaultPath = "/work/.agentsmith/coding-principles.md";
        var nestedDir = "/work/services/api-gateway/.agentsmith";
        var nestedFile = "/work/services/api-gateway/.agentsmith/coding-principles.md";

        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(defaultPath, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        reader.Setup(r => r.ExistsAsync(nestedFile, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        reader.Setup(r => r.ReadRequiredAsync(nestedFile, It.IsAny<CancellationToken>())).ReturnsAsync("# Sub Rules");
        reader.Setup(r => r.ListAsync("/work", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                "/work/services",
                "/work/services/api-gateway",
                nestedDir,
            });

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

        // CodingPrinciples is an alias for DomainRules — both resolve to same key
        pipeline.Get<string>(ContextKeys.CodingPrinciples).Should().Be("# Rules");
    }

    private static LoadCodingPrinciplesHandler MakeHandler(ISandboxFileReader reader)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader);
        return new LoadCodingPrinciplesHandler(
            new ProjectMetaResolver(),
            factory.Object,
            NullLogger<LoadCodingPrinciplesHandler>.Instance);
    }

    private static PipelineContext MakePipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        return pipeline;
    }
}
