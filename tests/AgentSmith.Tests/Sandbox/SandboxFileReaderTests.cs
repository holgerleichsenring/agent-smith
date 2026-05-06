using System.Text.Json;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

public sealed class SandboxFileReaderTests
{
    [Fact]
    public async Task ExistsAsync_FilePresent_ReturnsTrue()
    {
        var sandbox = MakeSandbox(StepKind.ReadFile, MakeResult(exitCode: 0, output: "content"));
        var reader = new SandboxFileReader(sandbox.Object);

        var exists = await reader.ExistsAsync("/work/foo.md", CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_FileMissing_ReturnsFalse()
    {
        var sandbox = MakeSandbox(StepKind.ReadFile, MakeResult(exitCode: 1, error: "file not found"));
        var reader = new SandboxFileReader(sandbox.Object);

        var exists = await reader.ExistsAsync("/work/missing.md", CancellationToken.None);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task TryReadAsync_FilePresent_ReturnsContent()
    {
        var sandbox = MakeSandbox(StepKind.ReadFile, MakeResult(exitCode: 0, output: "hello"));
        var reader = new SandboxFileReader(sandbox.Object);

        var content = await reader.TryReadAsync("/work/foo.md", CancellationToken.None);

        content.Should().Be("hello");
    }

    [Fact]
    public async Task TryReadAsync_FileMissing_ReturnsNull()
    {
        var sandbox = MakeSandbox(StepKind.ReadFile, MakeResult(exitCode: 1, error: "file not found"));
        var reader = new SandboxFileReader(sandbox.Object);

        var content = await reader.TryReadAsync("/work/missing.md", CancellationToken.None);

        content.Should().BeNull();
    }

    [Fact]
    public async Task ReadRequiredAsync_FilePresent_ReturnsContent()
    {
        var sandbox = MakeSandbox(StepKind.ReadFile, MakeResult(exitCode: 0, output: "hello"));
        var reader = new SandboxFileReader(sandbox.Object);

        var content = await reader.ReadRequiredAsync("/work/foo.md", CancellationToken.None);

        content.Should().Be("hello");
    }

    [Fact]
    public async Task ReadRequiredAsync_FileMissing_ThrowsFileNotFoundException()
    {
        var sandbox = MakeSandbox(StepKind.ReadFile, MakeResult(exitCode: 1, error: "file not found"));
        var reader = new SandboxFileReader(sandbox.Object);

        var act = () => reader.ReadRequiredAsync("/work/missing.md", CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task WriteAsync_Success_InvokesWriteFileStepWithContent()
    {
        Step? capturedStep = null;
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), null, It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) => capturedStep = step)
            .ReturnsAsync(MakeResult(exitCode: 0));
        var reader = new SandboxFileReader(sandbox.Object);

        await reader.WriteAsync("/work/result.md", "body", CancellationToken.None);

        capturedStep.Should().NotBeNull();
        capturedStep!.Kind.Should().Be(StepKind.WriteFile);
        capturedStep.Path.Should().Be("/work/result.md");
        capturedStep.Content.Should().Be("body");
    }

    [Fact]
    public async Task WriteAsync_Failure_ThrowsIOException()
    {
        var sandbox = MakeSandbox(StepKind.WriteFile, MakeResult(exitCode: 1, error: "permission denied"));
        var reader = new SandboxFileReader(sandbox.Object);

        var act = () => reader.WriteAsync("/work/result.md", "body", CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task ListAsync_Success_ParsesJsonArray()
    {
        var json = JsonSerializer.Serialize(new[] { "a.md", "b.md" });
        var sandbox = MakeSandbox(StepKind.ListFiles, MakeResult(exitCode: 0, output: json));
        var reader = new SandboxFileReader(sandbox.Object);

        var entries = await reader.ListAsync("/work", maxDepth: 2, CancellationToken.None);

        entries.Should().BeEquivalentTo(new[] { "a.md", "b.md" });
    }

    [Fact]
    public async Task ListAsync_PassesMaxDepthOnTheStep()
    {
        Step? capturedStep = null;
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), null, It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) => capturedStep = step)
            .ReturnsAsync(MakeResult(exitCode: 0, output: "[]"));
        var reader = new SandboxFileReader(sandbox.Object);

        await reader.ListAsync("/work", maxDepth: 4, CancellationToken.None);

        capturedStep!.Kind.Should().Be(StepKind.ListFiles);
        capturedStep.MaxDepth.Should().Be(4);
    }

    [Fact]
    public async Task ListAsync_NonZeroExit_ReturnsEmpty()
    {
        var sandbox = MakeSandbox(StepKind.ListFiles, MakeResult(exitCode: 1, error: "not a directory"));
        var reader = new SandboxFileReader(sandbox.Object);

        var entries = await reader.ListAsync("/work/nope", maxDepth: 2, CancellationToken.None);

        entries.Should().BeEmpty();
    }

    private static Mock<ISandbox> MakeSandbox(StepKind expectedKind, StepResult result)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.Is<Step>(step => step.Kind == expectedKind),
                It.IsAny<IProgress<StepEvent>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return sandbox;
    }

    private static StepResult MakeResult(int exitCode, string? output = null, string? error = null) =>
        new(StepResult.CurrentSchemaVersion, Guid.NewGuid(), exitCode,
            TimedOut: false, DurationSeconds: 0.01, ErrorMessage: error, OutputContent: output);
}
