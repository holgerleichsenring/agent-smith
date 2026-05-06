using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class AcquireSourceHandlerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"ast-{Guid.NewGuid():N}");
    private readonly AcquireSourceHandler _sut = new(
        new SandboxFileReaderFactory(),
        NullLogger<AcquireSourceHandler>.Instance);

    public AcquireSourceHandlerTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static Mock<ISandbox> MakeSandboxMock()
    {
        var mock = new Mock<ISandbox>();
        mock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(
                StepResult.CurrentSchemaVersion, Guid.NewGuid(), 0,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null));
        return mock;
    }

    [Fact]
    public async Task ExecuteAsync_CopiesFileToWorkspace_SetsRepository()
    {
        var sourceFile = Path.Combine(_tempDir, "contract.pdf");
        await File.WriteAllTextAsync(sourceFile, "fake pdf content");

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SourceFilePath, sourceFile);
        pipeline.Set(ContextKeys.Sandbox, MakeSandboxMock().Object);
        var context = new AcquireSourceContext(new SourceConfig { Type = "LocalFolder" }, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var repo = pipeline.Get<AgentSmith.Domain.Entities.Repository>(ContextKeys.Repository);
        repo.Should().NotBeNull();
        repo.LocalPath.Should().Be("/work");
    }

    [Fact]
    public async Task ExecuteAsync_MissingFile_ReturnsFail()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SourceFilePath, "/nonexistent/file.pdf");
        pipeline.Set(ContextKeys.Sandbox, MakeSandboxMock().Object);
        var context = new AcquireSourceContext(new SourceConfig(), pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }
}
