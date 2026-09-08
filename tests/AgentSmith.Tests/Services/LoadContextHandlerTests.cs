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
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
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
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var sut = MakeHandler(reader.Object);

        var context = CreateContext();
        await sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<string>(ContextKeys.ProjectContext, out _).Should().BeFalse();
    }

    // 2026-09-04-cf3d: two contexts sharing one sandbox both reach the master, each under
    // the context it came from — run a109 showed the frontend context's file never read.
    [Fact]
    public async Task ExecuteAsync_TwoContextsInOneSandbox_LoadsBothLabelledByContext()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync("/work/.agentsmith/contexts/backend/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("meta:\n  purpose: backend api");
        reader.Setup(r => r.TryReadAsync("/work/.agentsmith/contexts/frontend/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("meta:\n  purpose: frontend app");
        var sut = MakeHandler(reader.Object);
        var context = CreateContext();
        WithTwoContexts(context.Pipeline);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var stored = context.Pipeline.Get<string>(ContextKeys.ProjectContext);
        stored.Should().Contain("## Context: backend (workdir: backend)")
            .And.Contain("meta:\n  purpose: backend api")
            .And.Contain("## Context: frontend (workdir: frontend)")
            .And.Contain("meta:\n  purpose: frontend app");
        stored.IndexOf("backend api", StringComparison.Ordinal).Should()
            .BeLessThan(stored.IndexOf("frontend app", StringComparison.Ordinal), "sandbox context order is kept");
    }

    [Fact]
    public async Task ExecuteAsync_TwoContextsOneMissingItsFile_LoadsTheOtherVerbatim()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync("/work/.agentsmith/contexts/backend/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("meta:\n  purpose: backend api");
        reader.Setup(r => r.TryReadAsync("/work/.agentsmith/contexts/frontend/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var sut = MakeHandler(reader.Object);
        var context = CreateContext();
        WithTwoContexts(context.Pipeline);

        await sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.Get<string>(ContextKeys.ProjectContext).Should().Be("meta:\n  purpose: backend api",
            "one document renders unlabelled, as a single-context sandbox always did");
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

    private static Mock<ISandboxFileReader> NewReaderWithMeta(string contextYaml)
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync("/work/.agentsmith/contexts/default/context.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contextYaml);
        return reader;
    }

    private static LoadContextHandler MakeHandler(ISandboxFileReader reader)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader);
        return new LoadContextHandler(
            new ContextDocumentReader(factory.Object),
            new NoOpSystemEventPublisher(),
            new AsyncLocalRunContextAccessor(),
            new SandboxTargets(), NullLogger<LoadContextHandler>.Instance);
    }

    private LoadContextContext CreateContext()
    {
        var repo = new Repository(new BranchName("feature/test"), "https://github.com/test/test");
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
        return new LoadContextContext(repo, pipeline);
    }
}
