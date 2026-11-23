using AgentSmith.Contracts.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.ContractsCoverage;

/// <summary>2026-11-23-c81ea: the delete form, the two typed refusals and the bounded folder listing.</summary>
public sealed class BranchWriteContractTests
{
    [Fact]
    public void Deletion_IsADeleteWithNoContent_PositionalFormUnchanged()
    {
        var deletion = RepoFile.Deletion("deploy/a.yaml");

        deletion.Delete.Should().BeTrue();
        deletion.Content.Should().BeEmpty();
        new RepoFile("a", "b").Delete.Should().BeFalse();
        new RepoFile("a", "b").Should().Be(new RepoFile("a", "b"));
    }

    [Fact]
    public void Conflicted_IsNotWritten_AndCarriesTheReason()
    {
        var result = BranchWriteResult.Conflicted("moved");

        result.Conflict.Should().BeTrue();
        result.Written.Should().BeFalse();
        result.AlreadyHeld.Should().BeFalse();
        result.Error.Should().Be("moved");
        BranchWriteResult.Failed("x").Conflict.Should().BeFalse();
    }

    [Fact]
    public void HeldAlready_IsNotWritten_AndCarriesTheNotes()
    {
        var result = BranchWriteResult.HeldAlready(["gone"]);

        result.AlreadyHeld.Should().BeTrue();
        result.Written.Should().BeFalse();
        result.Conflict.Should().BeFalse();
        result.Error.Should().Be(BranchWriteResult.AllDeletionsAbsent);
        result.Notes.Should().Equal("gone");
    }

    [Fact]
    public void Bounded_OverTheBound_TakesTheBoundAndTruncates_ElseKeepsTheHostFlag()
    {
        RepoFileListing.Bounded(["a", "b", "c"], 2, hostTruncated: false).Should()
            .BeEquivalentTo(new RepoFileListing(["a", "b"], true));
        RepoFileListing.Bounded(["a"], 2, hostTruncated: true).Truncated.Should().BeTrue();
        RepoFileListing.Bounded(["a"], 2, hostTruncated: false).Truncated.Should().BeFalse();
        RepoFileListing.Empty.Files.Should().BeEmpty();
    }
}
