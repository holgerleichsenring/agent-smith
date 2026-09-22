using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-22-6ad7: the reader answers WHY it found no set. Four situations used to share one
/// null, and the caller's answer to two of them differs: an unwritten branch is handed the
/// approval record's set once, a branch it could not read is handed back.
/// </summary>
public sealed class SpecSetOnBranchTests
{
    private const string Key = "azdo-19106";

    [Fact]
    public async Task SpecSetReader_NothingAtThePath_SaysNothingIsAtThePath()
    {
        var result = await ReadAsync(new SpecBranchFiles { Key = Key });

        result.State.Should().Be(SpecSetBranchState.NothingAtThePath);
        result.Read.Should().BeNull();
    }

    [Fact]
    public async Task SpecSetReader_ASetYamlThatDoesNotParse_IsUnreadableRatherThanAbsent()
    {
        var branch = new SpecBranchFiles { Key = Key };
        branch.Seed($".agentsmith/specs/{Key}/set.yaml", "key: [this is not\n  a document");

        var result = await ReadAsync(branch);

        result.State.Should().Be(SpecSetBranchState.Unreadable,
            "a file that is there and broke is an edit gone wrong, not a hand-off that never happened");
        result.Why.Should().Contain("did not parse");
    }

    [Fact]
    public async Task SpecSetReader_AListedPhaseFileThatIsNotThere_IsUnreadable()
    {
        var branch = new SpecBranchFiles { Key = Key };
        branch.SeedSet(
            $"key: {Key}\nsource: Approved\nphases:\n- p19106a-onthebranch\n",
            new Dictionary<string, string>());

        var result = await ReadAsync(branch);

        result.State.Should().Be(SpecSetBranchState.Unreadable);
        result.Why.Should().Contain("p19106a-onthebranch");
    }

    /// <summary>
    /// The path was never LOOKED at, so "nothing is there" is a claim this run cannot make — and
    /// a branch it cannot see may well carry the edit a copy would hide.
    /// </summary>
    [Fact]
    public async Task SpecSetReader_NoSandboxForTheCarryingRepository_IsUnreadableRatherThanAbsent()
    {
        var result = await ReadAsync(new SpecBranchFiles { Key = Key }, sandboxed: false);

        result.State.Should().Be(SpecSetBranchState.Unreadable);
        result.Why.Should().Contain("primary").And.Contain("not checked out");
    }

    private static async Task<SpecSetOnBranch> ReadAsync(
        SpecBranchFiles branch, bool sandboxed = true)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.Empty, 0, false, 0, null, "branch-sha"));
        var readers = new Mock<ISandboxFileReaderFactory>();
        readers.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(branch);
        var pipeline = new PipelineContext();
        if (sandboxed)
            pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["primary"] = sandbox.Object });
        var reader = new SpecSetReader(
            readers.Object,
            new SandboxGitOperations(
                new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance, readers.Object,
                new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
            new SpecSetPhaseFileReader(
                new PhaseDraftReader(), NullLogger<SpecSetPhaseFileReader>.Instance),
            new SpecSetIndex(), new SandboxTargets(), NullLogger<SpecSetReader>.Instance);
        return await reader.ReadAsync(
            pipeline, new RepoConnection { Name = "primary" }, new SpecSetKey(Key), default);
    }
}
