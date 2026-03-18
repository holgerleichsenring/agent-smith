using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

public sealed class AcquireSourceHandlerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"ast-{Guid.NewGuid():N}");
    private readonly AcquireSourceHandler _sut = new(NullLogger<AcquireSourceHandler>.Instance);

    public AcquireSourceHandlerTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_CopiesFileToWorkspace_SetsRepository()
    {
        var sourceFile = Path.Combine(_tempDir, "contract.pdf");
        await File.WriteAllTextAsync(sourceFile, "fake pdf content");

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SourceFilePath, sourceFile);
        var context = new AcquireSourceContext(new SourceConfig { Type = "LocalFolder" }, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var repo = pipeline.Get<AgentSmith.Domain.Entities.Repository>(ContextKeys.Repository);
        repo.Should().NotBeNull();
        repo.LocalPath.Should().NotBeNullOrEmpty();
        File.Exists(Path.Combine(repo.LocalPath, "contract.pdf")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_MissingFile_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SourceFilePath, "/nonexistent/file.pdf");
        var context = new AcquireSourceContext(new SourceConfig(), pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }
}
