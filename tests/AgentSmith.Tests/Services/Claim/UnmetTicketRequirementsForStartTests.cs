using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Services.Claim;

/// <summary>
/// 2026-11-17-9a39a: the question a start asks. on_merge waits for the landing; on_green_push lifts a requirement whose
/// prerequisite pushed a green branch and names it as the base; a dependent that already ran is not held again.
/// </summary>
public sealed class UnmetTicketRequirementsForStartTests
{
    private readonly InMemoryTicketFacts _facts = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new() { CallBase = true }; // 9719d: the set reads loop LatestAsync

    public UnmetTicketRequirementsForStartTests() => _facts.Edges.Add(Edge("42", "41"));

    [Fact]
    public async Task ForStartAsync_OnMerge_AGreenPrerequisiteStillHolds()
    {
        Attempt("41", RunStatuses.Success);

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnMerge), "jira-42", default);

        hold.Unmet.Select(e => e.RequiredTicketId).Should().Equal("41");
        hold.StackedOn.Should().BeNull();
    }

    [Fact]
    public async Task ForStartAsync_OnGreenPush_AGreenPrerequisiteIsTheBase()
    {
        Attempt("41", RunStatuses.Success);

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().BeEmpty();
        hold.StackedOn!.RequiredTicketId.Should().Be("41");
    }

    [Fact]
    public async Task ForStartAsync_OnGreenPush_ARedPrerequisiteHolds()
    {
        Attempt("41", RunStatuses.Failed);

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().ContainSingle();
        hold.GreenLifts.Should().BeTrue();
    }

    [Fact]
    public async Task ForStartAsync_ADependentThatRanAlready_IsNotHeldWhileItsPrerequisiteReworks()
    {
        Attempt("41", RunStatuses.Failed);
        Attempt("42", RunStatuses.Success);

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().BeEmpty();
        hold.StackedOn!.RequiredTicketId.Should().Be("41", "it was cut from 41's branch and keeps building on it");
    }

    [Fact]
    public async Task ForStartAsync_ALandedPrerequisite_IsMetWithoutABase()
    {
        _facts.Land("41");

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().BeEmpty();
        hold.StackedOn.Should().BeNull();
    }

    [Fact]
    public async Task ForStartAsync_TwoIndependentGreenPrerequisites_NamesOneMergeThatUnblocks()
    {
        // 2026-11-22-6d85d: neither carries the other, so no tip — merging one leaves the other as the base.
        _facts.Edges.Add(Edge("42", "40"));
        Attempt("41", RunStatuses.Success);
        Attempt("40", RunStatuses.Success);

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().HaveCount(2);
        hold.MergesToStart.Should().ContainSingle();
        RequiresUnmetGate.Sentence(hold).Should().StartWith($"This ticket starts once #{hold.MergesToStart[0].RequiredTicketId} is merged");
    }

    [Fact]
    public async Task ForStartAsync_ThreeIndependentGreenPrerequisites_NamesTwoMerges()
    {
        _facts.Edges.Add(Edge("42", "40"));
        _facts.Edges.Add(Edge("42", "39"));
        foreach (var id in new[] { "41", "40", "39" }) Attempt(id, RunStatuses.Success);

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.MergesToStart.Should().HaveCount(2);
        RequiresUnmetGate.Sentence(hold).Should().MatchRegex("^This ticket starts once #\\d+ and #\\d+ are merged");
    }

    [Fact]
    public async Task ForStartAsync_APrerequisiteChainReachesTheOther_NamesOnlyTheMergeItDoesNotCarry()
    {
        // 40 requires 39; 41 is independent: stacking on 40 carries 39, so only 41's merge remains.
        _facts.Edges.Add(Edge("42", "40"));
        _facts.Edges.Add(Edge("42", "39"));
        _facts.Edges.Add(Edge("40", "39"));
        foreach (var id in new[] { "41", "40", "39" }) Attempt(id, RunStatuses.Success);

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.MergesToStart.Select(e => e.RequiredTicketId).Should().Equal("41");
    }

    [Fact]
    public async Task ForStartAsync_OneOfTwoPrerequisitesNotGreen_NamesNoMerge()
    {
        _facts.Edges.Add(Edge("42", "40"));
        Attempt("41", RunStatuses.Success);
        Attempt("40", RunStatuses.Failed);

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.MergesToStart.Should().BeEmpty();
        RequiresUnmetGate.Sentence(hold).Should().Be(RequiresUnmetGate.Sentence(hold.Unmet, greenLifts: true));
    }

    [Fact]
    public async Task ForStartAsync_TwoSiblingsStackedOnAnUnlandedBase_NamesTheBaseFirst()
    {
        // A merge onto 39's branch lands only with 39 (2026-11-22-6d85b), so 39's merge is named before 40's.
        _facts.Edges.Add(Edge("42", "40"));
        _facts.Edges.Add(Edge("41", "39"));
        _facts.Edges.Add(Edge("40", "39"));
        foreach (var id in new[] { "41", "40", "39" }) Attempt(id, RunStatuses.Success);

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.MergesToStart.Select(e => e.RequiredTicketId).Should().Equal("39", "40");
        RequiresUnmetGate.Sentence(hold).Should().StartWith("This ticket starts once #39 and #40 are merged, in that order");
    }

    [Fact]
    public void MergesNamed_JoinsWithCommasAndAnd() =>
        RequiresUnmetGate.MergesNamed(["#1", "#2", "#3"]).Should().Be("#1, #2 and #3");

    private UnmetTicketRequirements Read() =>
        new(_facts, _facts, new InMemorySpecApprovalStore(), new GreenPrerequisites(_facts, _attempts.Object, _facts));

    private void Attempt(string ticket, string status) =>
        _attempts.Setup(a => a.LatestAsync(TicketRequirementEdges.Project, ticket, null, default))
            .ReturnsAsync(new PreviousAttempt("run-" + ticket, status, DateTimeOffset.UnixEpoch, Finished: true) { OpenedPullRequest = true });

    private static ResolvedProject Project(RequiresStart mode) => new()
    {
        Name = TicketRequirementEdges.Project, Tracker = new TrackerConnection { Name = Tracker }, RequiresStart = mode,
    };
}
