using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-17-7c9ba: a plain re-trigger over the branch set as it is would write a revision about nothing;
/// every input, every change and every hand-back still writes one.
/// </summary>
public sealed class SpecSetUnchangedTests
{
    private static readonly SpecSetUnchanged Unchanged = new(new SeriesFiles(new SeriesManifest()));
    private static readonly SpecRevision Filed = new(1, "approved in design conversation session-1", ApprovedSets.Noon);

    [Fact]
    public void Holds_RetriggerOverTheBranchSet_True()
    {
        Unchanged.Holds(Next(Branch()), Read(Branch()), Decision(), questionPinned: false, preview: false)
            .Should().BeTrue();
    }

    [Fact]
    public void Holds_ChangedSpec_False()
    {
        var changed = Branch() with { Specs = [ApprovedSets.Spec("p0001a", "Another goal")] };

        Unchanged.Holds(Next(changed), Read(Branch()), Decision(), false, false).Should().BeFalse();
    }

    [Theory]
    [InlineData(SpecRevisionCause.Resume)]
    [InlineData(SpecRevisionCause.Comment)]
    [InlineData(SpecRevisionCause.TicketEdit)]
    [InlineData(SpecRevisionCause.ReviewerEdit)]
    public void Holds_ACauseOtherThanAPlainRetrigger_False(string cause)
    {
        Unchanged.Holds(Next(Branch()), Read(Branch()), Decision(cause), false, false).Should().BeFalse();
    }

    [Fact]
    public void Holds_ModelRanOrDiscardedOrPinnedOrPreview_False()
    {
        var set = Next(Branch());
        var read = Read(Branch());

        Unchanged.Holds(set, read, Decision() with { NeedsModel = true }, false, false).Should().BeFalse();
        Unchanged.Holds(set, read, Decision() with { Discarded = "an executed spec's edit" }, false, false).Should().BeFalse();
        Unchanged.Holds(set, read, Decision(), questionPinned: true, preview: false).Should().BeFalse();
        Unchanged.Holds(set, read, Decision(), questionPinned: false, preview: true).Should().BeFalse();
    }

    [Fact]
    public void Holds_HandedBackSetOrNoPrevious_False()
    {
        var handedBack = Branch() with { Handback = new SpecHandback(SpecHandbackCase.Question, "which one?") };

        Unchanged.Holds(Next(handedBack), Read(handedBack), Decision(), false, false).Should().BeFalse();
        Unchanged.Holds(Next(Branch()), null, Decision(), false, false).Should().BeFalse();
    }

    private static SpecSet Branch() =>
        ApprovedSets.Set("jira-1", [ApprovedSets.Spec("p0001a")], ApprovedSets.Approval(ApprovedSets.Noon))
            with { Revisions = [Filed], Series = "2026-11-17-0a0a" };

    private static SpecSet Next(SpecSet set) =>
        set with { Revisions = [.. set.Revisions, new SpecRevision(2, SpecRevisionCause.Retrigger, DateTimeOffset.UtcNow)] };

    private static SpecSetReadResult Read(SpecSet set) => new(set, "branch-sha");

    private static SpecSourceResolver.Decision Decision(string cause = SpecRevisionCause.Retrigger) =>
        new(SpecSource.BranchArtifact, Branch(), NeedsModel: false, Cause: cause);
}
