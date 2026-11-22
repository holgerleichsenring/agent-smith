using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Services.Claim;

/// <summary>
/// 2026-11-22-9719d: the start rule for many tickets answers each exactly as the one-ticket question does — green,
/// red, closed, landed, started, chained, independent and unrequired tickets side by side — and reads each
/// prerequisite's end once through the set read, never through the full newest-attempt read.
/// </summary>
public sealed class UnmetTicketRequirementsForStartManyTests
{
    private readonly InMemoryTicketFacts _facts = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new() { CallBase = true };

    public UnmetTicketRequirementsForStartManyTests()
    {
        _facts.Edges.AddRange([Edge("10", "1"), Edge("11", "2"), Edge("12", "3"), Edge("13", "4"), Edge("14", "2"),
            Edge("15", "1"), Edge("15", "5"), Edge("16", "1"), Edge("16", "6"), Edge("5", "1")]);
        Attempt("1", RunStatuses.Success);
        Attempt("2", RunStatuses.Failed);
        Attempt("3", RunStatuses.Success);
        Attempt("5", RunStatuses.Success);
        Attempt("6", RunStatuses.Success);
        Attempt("14", RunStatuses.Success);
        _facts.Closed.Add(new TicketClosedMark(Tracker, "jira-3", "3", TicketRequirementEdges.Project, DateTimeOffset.UnixEpoch, "sample.user", null, [], false));
        _facts.Land("4");
    }

    public static TheoryData<RequiresStart> Modes => new() { RequiresStart.OnGreenPush, RequiresStart.OnMerge };

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ForStartManyAsync_EveryKey_AnswersWhatForStartAsyncAnswers(RequiresStart mode)
    {
        string[] keys = ["jira-10", "jira-11", "jira-12", "jira-13", "jira-14", "jira-15", "jira-16", "jira-99"];

        var many = await Read().ForStartManyAsync(Configured(mode), keys, default);

        many.Keys.Should().BeEquivalentTo(keys);
        foreach (var key in keys)
        {
            var one = await Read().ForStartAsync(Configured(mode), key, default);
            many[key].HasRequirements.Should().Be(one.HasRequirements, key);
            many[key].Unmet.Should().Equal(one.Unmet, key);
            many[key].StackedOn.Should().Be(one.StackedOn, key);
            many[key].GreenLifts.Should().Be(one.GreenLifts, key);
        }
    }

    [Fact]
    public async Task ForStartManyAsync_OnGreenPush_ReadsTheExpectedHolds()
    {
        var many = await Read().ForStartManyAsync(Configured(RequiresStart.OnGreenPush),
            ["jira-10", "jira-11", "jira-12", "jira-13", "jira-14", "jira-15", "jira-16"], default);

        many["jira-10"].StackedOn!.RequiredTicketId.Should().Be("1", "1 is green");
        many["jira-11"].Unmet.Should().ContainSingle("2 is red");
        many["jira-12"].Unmet.Should().ContainSingle("3 carries a closed mark");
        many["jira-13"].Unmet.Should().BeEmpty("4 landed");
        many["jira-13"].StackedOn.Should().BeNull();
        many["jira-14"].Unmet.Should().BeEmpty("14 started already and is not held again");
        many["jira-15"].StackedOn!.RequiredTicketId.Should().Be("5", "5's chain reaches 1");
        many["jira-16"].Unmet.Should().HaveCount(2, "1 and 6 are independent, so there is no tip");
    }

    [Fact]
    public async Task ForStartManyAsync_ReadsEachEndOnce_AndNeverTheFullAttempt()
    {
        var ends = new List<string>();
        _attempts.Setup(a => a.LatestEndsAsync(TicketRequirementEdges.Project, It.IsAny<IReadOnlyCollection<string>>(), default))
            .Callback<string, IReadOnlyCollection<string>, CancellationToken>((_, ids, _) => ends.AddRange(ids))
            .ReturnsAsync(new Dictionary<string, PreviousAttempt>());
        _attempts.Setup(a => a.AttemptedAsync(TicketRequirementEdges.Project, It.IsAny<IReadOnlyCollection<string>>(), default))
            .ReturnsAsync(new HashSet<string>());

        await Read().ForStartManyAsync(Configured(RequiresStart.OnGreenPush), ["jira-10", "jira-15", "jira-16"], default);

        ends.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(["1", "5", "6"]);
        _attempts.Verify(a => a.LatestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GreenPrerequisites_StartedAsync_NamesTheTicketsWithAnAttempt()
    {
        var started = await new GreenPrerequisites(_facts, _attempts.Object, _facts).StartedAsync(TicketRequirementEdges.Project, ["14", "10"], default);

        started.Should().BeEquivalentTo(["14"]);
    }

    [Fact]
    public async Task GreenReads_PrefetchAsync_ReadsEachPrerequisiteOnce()
    {
        var reads = new GreenPrerequisites(_facts, _attempts.Object, _facts).Reads(TicketRequirementEdges.Project);

        await reads.PrefetchAsync([Edge("10", "1"), Edge("15", "1")], default);
        await reads.PrefetchAsync([Edge("16", "1")], default);
        var tip = await reads.TipAsync([Edge("16", "1")], requireGreen: true, default);

        tip!.RequiredTicketId.Should().Be("1");
        _attempts.Verify(a => a.LatestEndsAsync(TicketRequirementEdges.Project, It.IsAny<IReadOnlyCollection<string>>(), default), Times.Once);
    }

    private UnmetTicketRequirements Read() =>
        new(_facts, _facts, new InMemorySpecApprovalStore(), new GreenPrerequisites(_facts, _attempts.Object, _facts));

    private void Attempt(string ticket, string status) =>
        _attempts.Setup(a => a.LatestAsync(TicketRequirementEdges.Project, ticket, null, default))
            .ReturnsAsync(new PreviousAttempt("run-" + ticket, status, DateTimeOffset.UnixEpoch, Finished: true) { OpenedPullRequest = true });

    private static ResolvedProject Configured(RequiresStart mode) => new()
    {
        Name = TicketRequirementEdges.Project, Tracker = new TrackerConnection { Name = Tracker }, RequiresStart = mode,
    };
}
