using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0439: a shortfall delivers verified work and nothing else. Every source path the
/// index carries beyond the verified head is restored from it or removed; the run record
/// stays; the result is proven by asking git again.
/// </summary>
public sealed class UnverifiedWorkReverterTests
{
    private const string Head = "abc123";

    private readonly UnverifiedWorkReverter _sut = new(
        new SandboxGitOperations(
            new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance,
            new StubSandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
        NullLogger<UnverifiedWorkReverter>.Instance);

    [Fact]
    public async Task Restore_NoVerifiedHead_RefusesWithoutTouchingTheTree()
    {
        var sandbox = new NameStatusSandbox("");

        var restored = await _sut.RestoreAsync(new PipelineContext(), "server", sandbox, CancellationToken.None);

        restored.Should().BeFalse("what was verified is unknown, so nothing can be delivered as verified");
        sandbox.Asked.Should().BeEmpty();
    }

    [Fact]
    public async Task Restore_NothingBeyondTheHead_IsTrueWithoutACheckoutOrRemoval()
    {
        var sandbox = new NameStatusSandbox("A\t.agentsmith/runs/r1/result.md\n");

        var restored = await _sut.RestoreAsync(Pipeline(), "server", sandbox, CancellationToken.None);

        restored.Should().BeTrue("the run record is not source");
        sandbox.Asked.Should().NotContain(a => a.StartsWith("checkout") || a.StartsWith("rm"));
    }

    [Fact]
    public async Task Restore_WorkBeyondTheHead_RestoresChangedPathsAndRemovesAddedOnes()
    {
        var sandbox = new NameStatusSandbox(
            "M\tsrc/Guard.cs\nA\tsrc/New.cs\nD\tsrc/Gone.cs\nR100\tsrc/Old.cs\tsrc/Renamed.cs\n"
            + "A\t.agentsmith/runs/r1/plan.md\nM\t.agentsmith/contexts/default/context.yaml\n",
            afterRevert: "");

        var restored = await _sut.RestoreAsync(Pipeline(), "server", sandbox, CancellationToken.None);

        restored.Should().BeTrue();
        sandbox.Asked.Should().Contain($"checkout {Head} -- src/Guard.cs src/Gone.cs src/Old.cs");
        sandbox.Asked.Should().Contain("rm -f -q -- src/New.cs src/Renamed.cs");
        sandbox.Asked.Where(a => a.StartsWith("diff --cached --name-status")).Should().HaveCount(2,
            "the result is proven by asking git the same question again");
    }

    [Fact]
    public async Task Restore_WorkStillBeyondTheHeadAfterwards_IsFalse()
    {
        var sandbox = new NameStatusSandbox("M\tsrc/Guard.cs\n", afterRevert: "M\tsrc/Guard.cs\n");

        var restored = await _sut.RestoreAsync(Pipeline(), "server", sandbox, CancellationToken.None);

        restored.Should().BeFalse("an answer that is not empty is not a delivery");
    }

    [Fact]
    public async Task Restore_GitFails_IsFalse()
    {
        var sandbox = new NameStatusSandbox("", exitCode: 128);

        var restored = await _sut.RestoreAsync(Pipeline(), "server", sandbox, CancellationToken.None);

        restored.Should().BeFalse();
    }

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.VerifiedHeads, new Dictionary<string, string> { ["server"] = Head });
        return pipeline;
    }

    /// <summary>Answers name-status with the first script until a checkout or rm ran,
    /// then with the second; every other git call succeeds silently.</summary>
    private sealed class NameStatusSandbox(string beyond, string? afterRevert = null, int exitCode = 0) : ISandbox
    {
        private bool _reverted;

        public List<string> Asked { get; } = [];

        public string JobId => "name-status";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            var args = string.Join(' ', step.Args ?? []);
            if (step.Command == "git" && !args.StartsWith("add") && !args.StartsWith("config")) Asked.Add(args);
            if (args.StartsWith("checkout") || args.StartsWith("rm")) _reverted = true;
            var output = args.StartsWith("diff --cached --name-status")
                ? (_reverted ? afterRevert ?? beyond : beyond)
                : string.Empty;
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode, false, 0.01, null, output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
