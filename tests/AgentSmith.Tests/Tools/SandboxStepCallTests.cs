using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>2026-10-13-3890d: a sandbox that does not answer in time is an error the model reads, not an exception.</summary>
public sealed class SandboxStepCallTests
{
    [Fact]
    public async Task ToolCall_SandboxTimeout_ReturnsErrorText()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Sandbox step 6b55 did not produce a result within 60s"));

        var text = await new SandboxStepRunner(sandbox.Object).ReadAsync("api/Program.cs", null, null, false, CancellationToken.None);

        text.Should().StartWith("Error:").And.Contain("within 60s").And.Contain("retry");
    }

    [Fact]
    public async Task ToolCall_Cancelled_StillThrows()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => new SandboxStepRunner(sandbox.Object).RunAsync("true", null, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
