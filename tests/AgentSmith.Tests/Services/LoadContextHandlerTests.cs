using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class LoadContextHandlerTests : IDisposable
{
    private readonly LoadContextHandler _sut;
    private readonly string _tempDir;

    public LoadContextHandlerTests()
    {
        _sut = new LoadContextHandler(NullLogger<LoadContextHandler>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-loadctx-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, ".agentsmith"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_FileExists_ReturnsOkWithCharCount()
    {
        var yaml = "meta:\n  project: test\nstate:\n  done: {}";
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), yaml);

        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Loaded project context");
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsOk()
    {
        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No context file found");
    }

    [Fact]
    public async Task ExecuteAsync_FileExists_StoresInPipeline()
    {
        var yaml = "meta:\n  project: test";
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), yaml);

        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<string>(ContextKeys.ProjectContext, out var stored).Should().BeTrue();
        stored.Should().Be(yaml);
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_DoesNotSetPipeline()
    {
        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<string>(ContextKeys.ProjectContext, out _).Should().BeFalse();
    }

    private LoadContextContext CreateContext()
    {
        var repo = new Repository(_tempDir, new BranchName("feature/test"), "https://github.com/test/test");
        return new LoadContextContext(repo, new PipelineContext());
    }
}
