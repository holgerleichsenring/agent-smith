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

public sealed class LoadMemoryIndexHandlerTests
{
    private const string IndexPath = "/work/.agentsmith/memory/MEMORY.md";

    [Fact]
    public async Task LoadMemoryIndex_InjectsIndexAtPlanTime_AbsentStoreEmptyNoError()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var context = MakeContext();
        var result = await MakeHandler(reader.Object).ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("an absent store is never an error");
        context.Pipeline.Get<string>(ContextKeys.MemoryIndex)
            .Should().BeEmpty("absent store = empty section");
    }

    [Fact]
    public async Task ExecuteAsync_StorePresent_PublishesIndexContent()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(IndexPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Memory index\n\n- [a](a.md) (project) — one line");

        var context = MakeContext();
        var result = await MakeHandler(reader.Object).ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Pipeline.Get<string>(ContextKeys.MemoryIndex)
            .Should().Contain("- [a](a.md) (project) — one line");
    }

    [Fact]
    public async Task ExecuteAsync_NoSandboxes_SkipsWithEmptyIndex()
    {
        var context = new LoadMemoryIndexContext(
            new Repository(new BranchName("main"), "https://example.com"), new PipelineContext());

        var result = await MakeHandler(Mock.Of<ISandboxFileReader>())
            .ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Pipeline.Get<string>(ContextKeys.MemoryIndex).Should().BeEmpty();
    }

    private static LoadMemoryIndexHandler MakeHandler(ISandboxFileReader reader)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader);
        return new LoadMemoryIndexHandler(factory.Object, new SandboxTargets(), NullLogger<LoadMemoryIndexHandler>.Instance);
    }

    private static LoadMemoryIndexContext MakeContext()
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
        return new LoadMemoryIndexContext(
            new Repository(new BranchName("main"), "https://example.com"), pipeline);
    }
}
