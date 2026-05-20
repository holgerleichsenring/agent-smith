using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

public sealed class FilesystemToolHostReadRangeTests
{
    [Fact]
    public async Task ReadFile_PassesStartLineAndLineCountToSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        Step? captured = null;
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((s, _, _) => captured = s)
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, "line content"));
        var host = new FilesystemToolHost(sandbox.Object);

        await host.ReadFile("foo.cs", start_line: 10, line_count: 5);

        captured.Should().NotBeNull();
        captured!.Kind.Should().Be(StepKind.ReadFile);
        captured.StartLine.Should().Be(10);
        captured.LineCount.Should().Be(5);
        captured.WithLineNumbers.Should().BeTrue();
    }

    [Fact]
    public async Task ReadFile_WithLineNumbersFalse_PassesFlagThroughToSandbox()
    {
        var sandbox = new Mock<ISandbox>();
        Step? captured = null;
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Callback<Step, IProgress<StepEvent>?, CancellationToken>((s, _, _) => captured = s)
            .ReturnsAsync(new StepResult(1, Guid.NewGuid(), 0, false, 0.1, null, "bare content"));
        var host = new FilesystemToolHost(sandbox.Object);

        await host.ReadFile("foo.cs", with_line_numbers: false);

        captured!.WithLineNumbers.Should().BeFalse();
    }
}
