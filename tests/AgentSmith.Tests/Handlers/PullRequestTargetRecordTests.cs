using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-13-a284: the values behind the target — what is recorded per repository, and
/// when an already-open pull request has to be moved.
/// </summary>
public sealed class PullRequestTargetRecordTests
{
    [Fact]
    public void Record_KeepsOneTargetPerRepository()
    {
        var pipeline = new PipelineContext();

        PullRequestTargets.Record(pipeline, "server", "agent-smith/4711");
        PullRequestTargets.Record(pipeline, "client", "agent-smith/4712");

        PullRequestTargets.For(pipeline, "server")!.Value.Should().Be("agent-smith/4711");
        PullRequestTargets.For(pipeline, "client")!.Value.Should().Be("agent-smith/4712");
    }

    [Fact]
    public void Record_FellThrough_RecordsNothing_SoTheProviderResolvesItsOwnDefault()
    {
        var pipeline = new PipelineContext();

        PullRequestTargets.Record(pipeline, "server", rung: null);

        PullRequestTargets.For(pipeline, "server").Should().BeNull(
            "an absent entry is what makes a run with no rung behave exactly as before");
    }

    [Fact]
    public void For_RepositoryNobodyRecorded_IsNull()
    {
        var pipeline = new PipelineContext();
        PullRequestTargets.Record(pipeline, "server", "agent-smith/4711");

        PullRequestTargets.For(pipeline, "client").Should().BeNull();
    }

    [Theory]
    [InlineData("main", "agent-smith/4711", true)]
    [InlineData("agent-smith/4711", "agent-smith/4711", false)]
    [InlineData(null, "agent-smith/4711", false)]
    [InlineData("", "agent-smith/4711", false)]
    public void NeedsMove_OnlyWhenAKnownBaseDiffersFromTheRung(string? current, string target, bool expected) =>
        PullRequestTargets.NeedsMove(current, new BranchName(target)).Should().Be(expected);

    [Fact]
    public void NeedsMove_NoRung_IsNever() =>
        PullRequestTargets.NeedsMove("main", target: null).Should().BeFalse();

    [Fact]
    public void Placement_ReadyOrStopped_AreTheTwoAnswers()
    {
        WorkBranchPlacement.On("agent-smith/4711").Should().BeEquivalentTo(
            new { Problem = (string?)null, Rung = "agent-smith/4711" });
        WorkBranchPlacement.Stop("the rung could not be checked out").Should().BeEquivalentTo(
            new { Problem = "the rung could not be checked out", Rung = (string?)null });
    }

    [Theory]
    [InlineData("nothing to commit, working tree clean", true)]
    [InlineData("No changes added to commit", true)]
    [InlineData("fatal: could not read Username", false)]
    public void EmptyCommit_ExplainsOnlyTheCommitThatHadNothingToDo(string message, bool expected) =>
        EmptyCommit.Explains(new InvalidOperationException(message)).Should().Be(expected);
}
