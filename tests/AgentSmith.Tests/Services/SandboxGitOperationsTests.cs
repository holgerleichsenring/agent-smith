using AgentSmith.Tests.TestHelpers;
using AgentSmith.Contracts.Services;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
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
    private readonly SandboxGitOperations _sut = new(NullLogger<SandboxGitOperations>.Instance, new StubSandboxFileReaderFactory());

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
        await _sut.CommitAndPushAsync(_sandboxMock.Object, "feat/branch", "msg", RepoType.GitHub, CancellationToken.None);

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

        var act = async () => await _sut.CommitAndPushAsync(_sandboxMock.Object, "branch", "msg", RepoType.GitHub, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains("nothing to commit"));
    }

    [Fact]
    public async Task ConsolidateSecondarySandboxes_MultiSandbox_AppliesSecondaryDiffIntoPrimary()
    {
        var primarySteps = new List<Step>();
        var primary = ScriptedSandbox(primarySteps, diffOutput: null);
        var secondarySteps = new List<Step>();
        var secondary = ScriptedSandbox(secondarySteps, diffOutput: "diff --git a/x b/x\n+change\n");

        var matches = new List<KeyValuePair<string, ISandbox>>
        {
            new("repo/primary", primary),
            new("repo/secondary", secondary),
        };

        var n = await _sut.ConsolidateSecondarySandboxesAsync(matches, primary, CancellationToken.None);

        n.Should().Be(1);
        secondarySteps.Should().Contain(s => (s.Args ?? Array.Empty<string>()).Contains("--cached"), "the secondary's staged diff is pulled");
        primarySteps.Should().Contain(s => (s.Args ?? Array.Empty<string>()).Contains("apply"), "the secondary's diff is git-applied into the primary");
    }

    [Fact]
    public async Task ConsolidateSecondarySandboxes_SingleSandbox_NoOp()
    {
        var n = await _sut.ConsolidateSecondarySandboxesAsync(
            new List<KeyValuePair<string, ISandbox>> { new("repo", _sandboxMock.Object) },
            _sandboxMock.Object, CancellationToken.None);

        n.Should().Be(0);
    }

    private static ISandbox ScriptedSandbox(List<Step> steps, string? diffOutput)
    {
        var mock = new Mock<ISandbox>();
        mock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                steps.Add(step);
                var output = (step.Args ?? Array.Empty<string>()).Contains("--cached") ? diffOutput : null;
                return Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, output));
            });
        return mock.Object;
    }

    [Fact]
    public async Task CommitAndPushAsync_PushFails_ThrowsWithUnderlyingError()
    {
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Args!.Contains("push")), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 128, false, 0.1, "non-fast-forward")));

        var act = async () => await _sut.CommitAndPushAsync(_sandboxMock.Object, "branch", "msg", RepoType.GitHub, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(e => e.Message.Contains("non-fast-forward"));
    }
}
