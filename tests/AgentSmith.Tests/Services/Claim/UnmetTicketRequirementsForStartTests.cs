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
        hold.StackedPerRepo.Should().BeEmpty();
    }

    [Fact]
    public async Task ForStartAsync_OnGreenPush_AGreenPrerequisiteIsTheBase()
    {
        Attempt("41", RunStatuses.Success);

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().BeEmpty();
        hold.StackedPerRepo.Should().Equal(new Dictionary<string, string> { ["api"] = "41" });
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
        hold.StackedPerRepo["api"].Should().Be("41", "it was cut from 41's branch and keeps building on it");
    }

    [Fact]
    public async Task ForStartAsync_ALandedPrerequisite_IsMetWithoutABase()
    {
        _facts.Land("41");

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().BeEmpty();
        hold.StackedPerRepo.Should().BeEmpty();
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

    // 2026-11-23-31dca: chain S1 <- S2(server, worker) <- S3(server); the dependent requires S3 only.
    [Fact]
    public async Task ForStartAsync_AChainAcrossRepos_StacksEachRepoOnItsNearestPrerequisite()
    {
        _facts.Edges.Clear();
        _facts.Edges.AddRange([Edge("42", "s3"), Edge("s3", "s2"), Edge("s2", "s1")]);
        Attempt("S1", RunStatuses.Success, "server", "worker");
        Attempt("S2", RunStatuses.Success, "server", "worker");
        Attempt("S3", RunStatuses.Success, "server");

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().BeEmpty("no repo has two independent prerequisites");
        hold.StackedPerRepo.Should().Equal(new Dictionary<string, string> { ["server"] = "S3", ["worker"] = "S2" });
    }

    [Fact]
    public async Task ForStartAsync_APrerequisiteWithWorkOnlyInAnotherRepo_DoesNotHold_AndLeavesTheOwnRepoUnstamped()
    {
        Attempt("41", RunStatuses.Success, "server");

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().BeEmpty();
        hold.StackedPerRepo.Should().Equal(new Dictionary<string, string> { ["server"] = "41" },
            "server is read at 41's branch; client, where the dependent works, cuts from its own base");
    }

    [Fact]
    public async Task ForStartAsync_TwoIndependentGreenPrerequisitesInDifferentRepos_DoNotHold()
    {
        _facts.Edges.Add(Edge("42", "40"));
        Attempt("41", RunStatuses.Success, "server");
        Attempt("40", RunStatuses.Success, "worker");

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().BeEmpty();
        hold.StackedPerRepo.Should().Equal(new Dictionary<string, string> { ["server"] = "41", ["worker"] = "40" });
    }

    [Fact]
    public async Task ForStartAsync_ADeeperPrerequisiteOfTheChainNotGreen_HoldsNamingIt()
    {
        _facts.Edges.Clear();
        _facts.Edges.AddRange([Edge("42", "s3"), Edge("s3", "s2")]);
        Attempt("S3", RunStatuses.Success, "server");
        Attempt("S2", RunStatuses.Failed, "worker");

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Select(e => e.RequiredTicketId).Should().Equal(["S2"], "S2's worker branch would be a base, and it is not green");
    }

    [Fact]
    public async Task ForStartAsync_APrerequisiteNotGreenYet_HoldsEvenWithItsReposUnknown()
    {
        _attempts.Setup(a => a.LatestAsync(TicketRequirementEdges.Project, "41", null, default))
            .ReturnsAsync(new PreviousAttempt("run-41", RunStatuses.Running, DateTimeOffset.UnixEpoch, Finished: false));

        var hold = await Read().ForStartAsync(Project(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Select(e => e.RequiredTicketId).Should().Equal("41");
        hold.StackedPerRepo.Should().BeEmpty();
    }

    [Fact]
    public void MergesNamed_JoinsWithCommasAndAnd() =>
        RequiresUnmetGate.MergesNamed(["#1", "#2", "#3"]).Should().Be("#1, #2 and #3");

    private UnmetTicketRequirements Read() =>
        new(_facts, _facts, new InMemorySpecApprovalStore(), new GreenPrerequisites(_facts, _attempts.Object, _facts, _facts));

    // 2026-11-23-31dca: the attempt's pull requests name the repos it worked in; one shared repo unless told otherwise.
    private void Attempt(string ticket, string status, params string[] repos) =>
        _attempts.Setup(a => a.LatestAsync(TicketRequirementEdges.Project, ticket, null, default))
            .ReturnsAsync(new PreviousAttempt("run-" + ticket, status, DateTimeOffset.UnixEpoch, Finished: true)
            {
                OpenedPullRequest = true,
                PullRequestUrls = (repos.Length == 0 ? ["api"] : repos).ToDictionary(r => r, r => $"https://git.test/o/{r}/pull/{ticket}"),
            });

    private static ResolvedProject Project(RequiresStart mode) => new()
    {
        Name = TicketRequirementEdges.Project, Tracker = new TrackerConnection { Name = Tracker }, RequiresStart = mode,
    };
}
