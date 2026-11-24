using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Series;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Services.Claim;

/// <summary>
/// 2026-11-24-f9c5b: a ticket of a series with a series branch waits for its prerequisites to land (on that branch) under
/// every requires_start, never stacks, and carries the branch into its claim; a ticket outside the series that requires one
/// of its tickets waits for the series to end.
/// </summary>
public sealed class SeriesBranchHoldTests
{
    private const string Branch = "agent-smith/series/sr-1";
    private readonly InMemoryTicketFacts _facts = new();
    private readonly InMemorySeriesRunStore _series = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new() { CallBase = true };

    public SeriesBranchHoldTests()
    {
        _facts.Edges.Add(Edge("42", "41"));
        _attempts.Setup(a => a.LatestAsync(Project, "41", null, default))
            .ReturnsAsync(new PreviousAttempt("run-41", RunStatuses.Success, DateTimeOffset.UnixEpoch, Finished: true)
            {
                OpenedPullRequest = true, PullRequestUrls = new Dictionary<string, string> { ["api"] = "https://x/api/41" },
            });
    }

    [Theory]
    [InlineData(RequiresStart.OnGreenPush)]
    [InlineData(RequiresStart.OnMerge)]
    public async Task ForStartAsync_ASeriesTicketWithAGreenUnlandedPrerequisite_IsHeldWithoutAStack(RequiresStart mode)
    {
        Series("41", "42");

        var hold = await Read().ForStartAsync(Configured(mode), "jira-42", default);

        hold.Unmet.Select(e => e.RequiredTicketId).Should().Equal("41");
        hold.StackedPerRepo.Should().BeEmpty();
        hold.SeriesBranch.Should().Be(Branch);
    }

    [Fact]
    public async Task ForStartAsync_ASeriesTicketWhosePrerequisiteLanded_IsFreeAndNamesTheBranch()
    {
        Series("41", "42");
        _facts.Land("41");

        var hold = await Read().ForStartAsync(Configured(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().BeEmpty();
        hold.SeriesBranch.Should().Be(Branch);
        (await Read().ForStartAsync(Configured(RequiresStart.OnMerge), "jira-41", default)).SeriesBranch
            .Should().Be(Branch, "a series ticket with no requirement is cut from the branch too");
    }

    [Fact]
    public async Task ForStartAsync_ATicketOutsideAnySeries_StacksAsBefore()
    {
        var hold = await Read().ForStartAsync(Configured(RequiresStart.OnGreenPush), "jira-42", default);

        hold.Unmet.Should().BeEmpty();
        hold.StackedPerRepo.Should().BeEquivalentTo(new Dictionary<string, string> { ["api"] = "41" });
        hold.SeriesBranch.Should().BeNull();
    }

    [Fact]
    public async Task OutsideDependent_OfALandedSeriesTicket_WaitsUntilTheSeriesEnds()
    {
        Series("41", "43");
        _facts.Land("41");

        (await Read().ForStartAsync(Configured(RequiresStart.OnMerge), "jira-42", default)).Unmet
            .Select(e => e.RequiredTicketId).Should().Equal("41");
        (await Read().OfAsync(Tracker, "jira-42", default)).Unmet.Should().ContainSingle("no merge of it lands first either");

        _series.Records["sr-1"] = _series.Records["sr-1"] with { State = SeriesRunStates.Done };
        (await Read().ForStartAsync(Configured(RequiresStart.OnMerge), "jira-42", default)).Unmet.Should().BeEmpty();
        (await Read().OfAsync(Tracker, "jira-42", default)).Unmet.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_AFreeSeriesTicket_IsStampedWithTheBranchAndNoStack()
    {
        Series("41", "42");
        _facts.Land("41");
        var queued = new ClaimRequest("jira", Project, new TicketId("42"), PipelinePresets.CodeName)
        {
            InitialContext = new() { [ContextKeys.StackedOnTicket] = "40" },
        };

        var (request, refusal) = await StackedBaseStamp.ApplyAsync(queued, Config(RequiresStart.OnGreenPush), Read(), default);

        refusal.Should().BeNull();
        request.InitialContext![ContextKeys.SeriesBranch].Should().Be(Branch);
        request.InitialContext.Should().NotContainKey(ContextKeys.StackedOnTicket);
    }

    [Fact]
    public async Task ApplyAsync_ATicketThatLeftNoSeries_DropsACarriedBranch()
    {
        var queued = new ClaimRequest("jira", Project, new TicketId("41"), PipelinePresets.CodeName)
        {
            InitialContext = new() { [ContextKeys.SeriesBranch] = Branch },
        };

        var (request, _) = await StackedBaseStamp.ApplyAsync(queued, Config(RequiresStart.OnMerge), Read(), default);

        request.InitialContext.Should().BeNull();
    }

    private void Series(params string[] tickets) =>
        _series.Records["sr-1"] = new SeriesRunRecord("sr-1", "conv", Project, "operator", true, SeriesRunStates.Running,
            [.. tickets.Select(t => new SeriesTicket(t, t, t, false, DateTimeOffset.UnixEpoch))], DateTimeOffset.UnixEpoch)
        {
            IntegrationBranch = Branch,
        };

    private UnmetTicketRequirements Read() =>
        new(_facts, _facts, new InMemorySpecApprovalStore(), new GreenPrerequisites(_facts, _attempts.Object, _facts, _facts), _series);

    private static ResolvedProject Configured(RequiresStart mode) =>
        new() { Name = Project, Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.Jira }, RequiresStart = mode };

    private static AgentSmithConfig Config(RequiresStart mode) =>
        new() { Projects = new Dictionary<string, ResolvedProject> { [Project] = Configured(mode) } };
}
