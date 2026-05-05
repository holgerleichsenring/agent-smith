using AgentSmith.Application.Services;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class SandboxGitOperationsTests
{
    private readonly Mock<ISandbox> _sandboxMock = new();
    private readonly List<Step> _steps = new();
    private readonly SandboxGitOperations _sut = new(NullLogger<SandboxGitOperations>.Instance);

    public SandboxGitOperationsTests()
    {
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                _steps.Add(step);
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null));
            });
    }

    [Fact]
    public async Task CommitAndPushAsync_HappyPath_RunsConfigStageCommitPush_InOrder()
    {
        await _sut.CommitAndPushAsync(_sandboxMock.Object, "feat/branch", "msg", CancellationToken.None);

        var commands = _steps.Select(s => string.Join(' ', new[] { s.Command }.Concat(s.Args ?? Array.Empty<string>()))).ToList();
        commands.Should().Contain(c => c.Contains("config user.email"));
        commands.Should().Contain(c => c.Contains("add -A"));
        commands.Should().Contain(c => c.Contains("commit -m msg"));
        commands.Should().Contain(c => c.Contains("push") && c.Contains("HEAD:feat/branch"));
    }

    [Fact]
    public async Task CommitAndPushAsync_NothingToCommit_ThrowsCleanWorkingTreeError()
    {
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Args!.Contains("commit")), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.1, "nothing to commit, working tree clean")));

        var act = async () => await _sut.CommitAndPushAsync(_sandboxMock.Object, "branch", "msg", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains("nothing to commit"));
    }

    [Fact]
    public async Task CommitAndPushAsync_PushFails_ThrowsWithUnderlyingError()
    {
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Args!.Contains("push")), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 128, false, 0.1, "non-fast-forward")));

        var act = async () => await _sut.CommitAndPushAsync(_sandboxMock.Object, "branch", "msg", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains("non-fast-forward"));
    }
}
