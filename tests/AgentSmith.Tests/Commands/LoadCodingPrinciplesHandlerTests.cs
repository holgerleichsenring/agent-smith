using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Events;
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
        var relativePath = ".agentsmith/principles.md";
        var fullPath = "/work/.agentsmith/principles.md";

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
        var defaultPath = "/work/.agentsmith/principles.md";
        var nestedFile = "/work/.agentsmith/contexts/default/principles.md";

        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(defaultPath, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        reader.Setup(r => r.TryReadAsync(nestedFile, It.IsAny<CancellationToken>())).ReturnsAsync("# Sub Rules");

        var handler = MakeHandler(reader.Object);
        var repo = new Repository(new BranchName("main"), "https://example.com");
        var pipeline = MakePipeline();
        var context = new LoadCodingPrinciplesContext(".agentsmith/principles.md", repo, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.DomainRules).Should().Be("# Sub Rules");
    }

    [Fact]
    public async Task ExecuteAsync_ContentAccessibleViaCodingPrinciplesAlias()
    {
        var relativePath = ".agentsmith/principles.md";
        var fullPath = "/work/.agentsmith/principles.md";

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

    // 2026-09-04-cf3d: two contexts sharing one sandbox both contribute their principles,
    // each under the context it governs — run a109's master never saw the frontend's.
    [Fact]
    public async Task ExecuteAsync_TwoContextsInOneSandbox_LoadsBothLabelledByContext()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync("/work/.agentsmith/principles.md", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        ServeNested(reader, "backend", "# Backend rules");
        ServeNested(reader, "frontend", "# Frontend rules");
        var handler = MakeHandler(reader.Object);
        var pipeline = MakePipeline();
        WithTwoContexts(pipeline);
        var context = new LoadCodingPrinciplesContext(".agentsmith/principles.md", new Repository(new BranchName("main"), "https://example.com"), pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var rules = pipeline.Get<string>(ContextKeys.DomainRules);
        rules.Should().Contain("## Context: backend (workdir: backend)")
            .And.Contain("# Backend rules")
            .And.Contain("## Context: frontend (workdir: frontend)")
            .And.Contain("# Frontend rules");
        rules.IndexOf("# Backend rules", StringComparison.Ordinal).Should()
            .BeLessThan(rules.IndexOf("# Frontend rules", StringComparison.Ordinal), "sandbox context order is kept");
    }

    [Fact]
    public async Task ExecuteAsync_TwoContextsButAFlatFile_LoadsTheFlatFileForTheSandbox()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync("/work/.agentsmith/principles.md", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        reader.Setup(r => r.ReadRequiredAsync("/work/.agentsmith/principles.md", It.IsAny<CancellationToken>())).ReturnsAsync("# Repo rules");
        var handler = MakeHandler(reader.Object);
        var pipeline = MakePipeline();
        WithTwoContexts(pipeline);
        var context = new LoadCodingPrinciplesContext(".agentsmith/principles.md", new Repository(new BranchName("main"), "https://example.com"), pipeline);

        await handler.ExecuteAsync(context, CancellationToken.None);

        pipeline.Get<string>(ContextKeys.DomainRules).Should().Be("# Repo rules",
            "the pre-contexts flat file speaks for the whole sandbox, unlabelled");
    }

    private static void ServeNested(Mock<ISandboxFileReader> reader, string contextName, string content)
    {
        var path = $"/work/.agentsmith/contexts/{contextName}/principles.md";
        reader.Setup(r => r.TryReadAsync(path, It.IsAny<CancellationToken>())).ReturnsAsync(content);
    }

    private static void WithTwoContexts(PipelineContext pipeline)
    {
        var backend = new RemoteContextDiscovery("backend", "backend", "typescript");
        var frontend = new RemoteContextDiscovery("frontend", "frontend", "typescript");
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal) { ["default"] = backend });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                ["default"] = [backend, frontend],
            });
    }

    private static LoadCodingPrinciplesHandler MakeHandler(ISandboxFileReader reader)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader);
        return new LoadCodingPrinciplesHandler(
            factory.Object,
            new ContextDocumentReader(factory.Object),
            new NoOpSystemEventPublisher(),
            new AsyncLocalRunContextAccessor(),
            new SandboxTargets(), NullLogger<LoadCodingPrinciplesHandler>.Instance);
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
