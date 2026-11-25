using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>2026-11-25-96aec: which facets of a series ticket's pull-request read wake its master, which are only recorded
/// (quiet), and which record nothing at all.</summary>
public sealed class SeriesPrChangeTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private const string Url = "https://host/o/r/pull/7";
    private const string Head = "abc1234ff";

    private static PrReviewRecordView Stored(string state = PrReviewStates.Open, string? ci = null, DateTimeOffset? ciAt = null,
        bool conflicts = false, string? target = "main", params PrReviewEntryView[] entries) =>
        new("k7", Url, "r", "7", "p", "7", "run-7", state, T, T, null, entries)
        {
            HeadSha = Head, Ci = ci is null ? null : new PrRecordCiView(ci, Head, ciAt ?? T, []), HasConflicts = conflicts,
            TargetBranch = target,
        };

    private static PrReviewRecord Read(string state = PrReviewStates.Open, string? ci = null, bool? conflicts = null,
        string? target = "main", DateTimeOffset? at = null) => new()
    {
        PrKey = "k7", Url = Url, State = state, HeadSha = Head, CiState = ci, CiHeadSha = ci is null ? null : Head,
        HasConflicts = conflicts, TargetBranch = target, CheckedAtTicks = (at ?? T.AddMinutes(1)).UtcTicks,
    };

    private static IReadOnlyList<SeriesWake> Of(PrReviewRecordView? stored, PrReviewRecord read, params PrReviewEntry[] entries) =>
        SeriesPrChange.Of(stored, read, entries, pendingSeconds: 3600);

    [Theory]
    [InlineData(CiStates.Green, "is green")]
    [InlineData(CiStates.Red, "is red")]
    [InlineData(CiStates.NotStarted, "has not started")]
    public void ACiVerdictOnTheHead_Wakes(string ci, string words) =>
        Of(Stored(ci: CiStates.Pending), Read(ci: ci)).Should().ContainSingle()
            .Which.Should().Match<SeriesWake>(w => w.Kind == SeriesWakeKinds.Ci && !w.Quiet && w.Text.Contains(words));

    [Theory]
    [InlineData(CiStates.Pending)]
    [InlineData(CiStates.None)]
    public void ARunningOrAbsentCi_RecordsNothing(string ci) =>
        Of(Stored(ci: CiStates.Pending), Read(ci: ci)).Should().BeEmpty();

    [Fact]
    public void TheSameVerdictReadAgain_RecordsNothing() =>
        Of(Stored(ci: CiStates.Green), Read(ci: CiStates.Green)).Should().BeEmpty();

    [Fact]
    public void AHeadThatMoved_RecordsNothing()
    {
        var pushed = Read(ci: CiStates.Pending);
        pushed.HeadSha = pushed.CiHeadSha = "ffff000";

        Of(Stored(ci: CiStates.Green), pushed).Should().BeEmpty("a push is no decision; its CI verdict will be");
    }

    [Theory]
    [InlineData(PrReviewStates.Merged)]
    [InlineData(PrReviewStates.Closed)]
    public void APullRequestMergedOrClosed_Wakes(string state) =>
        Of(Stored(), Read(state)).Should().ContainSingle().Which.Should()
            .Match<SeriesWake>(w => w.Kind == SeriesWakeKinds.PullRequest && !w.Quiet && w.Text.Contains($"is {state}"));

    [Fact]
    public void AReopenedPullRequest_Wakes() =>
        Of(Stored(PrReviewStates.Closed), Read()).Should().ContainSingle().Which.Quiet.Should().BeFalse();

    [Fact]
    public void TheFirstReadOfAnOpenPullRequest_IsQuiet() =>
        Of(null, Read()).Should().ContainSingle().Which.Should().Match<SeriesWake>(w => w.Quiet && w.Text.Contains("is open"));

    [Fact]
    public void AConflictAppearing_Wakes_AndClearing_IsQuiet()
    {
        Of(Stored(), Read(conflicts: true)).Should().ContainSingle().Which.Should()
            .Match<SeriesWake>(w => !w.Quiet && w.Text.Contains("conflicts with its base"));
        Of(Stored(conflicts: true), Read(conflicts: false)).Should().ContainSingle().Which.Should()
            .Match<SeriesWake>(w => w.Quiet && w.Text.Contains("no longer conflicts"));
    }

    [Fact]
    public void TheFirstGoodReadAfterAFailedOne_IsQuiet() =>
        Of(Stored(PrReviewStates.Unread), Read()).Should().ContainSingle().Which.Quiet.Should().BeTrue("the record never knew a state");

    [Fact]
    public void ATargetChange_IsQuiet() =>
        Of(Stored(), Read(target: "agent-smith/series/sr-1")).Should().ContainSingle().Which.Quiet.Should().BeTrue();

    [Fact]
    public void APersonsNewApprovalOrChangeRequest_Wakes_OursAndAStandingOneDoNot()
    {
        var approved = Entry("alice", PrReviewDecisions.Approved);
        Of(Stored(), Read(), approved).Should().ContainSingle().Which.Should()
            .Match<SeriesWake>(w => w.Kind == SeriesWakeKinds.Review && !w.Quiet && w.Text.Contains("is approved by alice"));
        Of(Stored(), Read(), Entry("bob", PrReviewDecisions.ChangesRequested)).Should().ContainSingle()
            .Which.Text.Should().Contain("has changes requested by bob");
        Of(Stored(), Read(), Entry("agent-smith", PrReviewDecisions.Approved, ours: true)).Should().BeEmpty();
        Of(Stored(entries: View("alice", PrReviewDecisions.Approved)), Read(), approved).Should().BeEmpty("it stood before");
    }

    [Fact]
    public void ACiRunningPastTheDeadline_WakesOncePerHead()
    {
        var late = Of(Stored(ci: CiStates.Pending, ciAt: T), Read(ci: CiStates.Pending, at: T.AddMinutes(61))).Should().ContainSingle().Subject;

        late.Should().Match<SeriesWake>(w => w.Kind == SeriesWakeKinds.Ci && !w.Quiet && w.Once == $"ci-deadline:k7:{Head}");
        late.Text.Should().Contain("61 minutes without a verdict").And.Contain("merge.ci_pending_seconds = 3600");
        Of(Stored(ci: CiStates.Pending, ciAt: T), Read(ci: CiStates.Pending, at: T.AddMinutes(59))).Should().BeEmpty();
    }

    private static PrReviewEntry Entry(string author, string decision, bool ours = false) =>
        new() { AuthorLogin = author, Decision = decision, Standing = true, IsOurs = ours };

    private static PrReviewEntryView View(string author, string decision) =>
        new("h1", PrReviewEntryKinds.Decision, decision, true, author, true, false, false, false, null, null, null, null, string.Empty, false, T);
}
