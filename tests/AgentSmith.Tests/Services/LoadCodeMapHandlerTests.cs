using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class LoadCodeMapHandlerTests : IDisposable
{
    private readonly LoadCodeMapHandler _sut;
    private readonly string _tempDir;

    public LoadCodeMapHandlerTests()
    {
        _sut = new LoadCodeMapHandler(NullLogger<LoadCodeMapHandler>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-loadmap-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_FileExists_LoadsContent()
    {
        var yaml = "modules:\n  - name: Core\n    path: src/Core";
        File.WriteAllText(Path.Combine(_tempDir, "code-map.yaml"), yaml);

        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Loaded code map");
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsOk()
    {
        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No code map found");
    }

    [Fact]
    public async Task ExecuteAsync_FileExists_StoresInPipeline()
    {
        var yaml = "modules:\n  - name: Core";
        File.WriteAllText(Path.Combine(_tempDir, "code-map.yaml"), yaml);

        var context = CreateContext();
        await _sut.ExecuteAsync(context);

        context.Pipeline.TryGet<string>(ContextKeys.CodeMap, out var stored).Should().BeTrue();
        stored.Should().Be(yaml);
    }

    private LoadCodeMapContext CreateContext()
    {
        var repo = new Repository(_tempDir, new BranchName("feature/test"), "https://github.com/test/test");
        return new LoadCodeMapContext(repo, new PipelineContext());
    }
}
