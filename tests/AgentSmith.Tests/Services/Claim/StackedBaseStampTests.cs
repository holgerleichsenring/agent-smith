using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Services.Claim;

/// <summary>
/// 2026-11-17-9a39b: under the claim lock a code claim is asked once more — a green tip is stamped as the run's base,
/// a requirement that turned unmet since the gate refuses, a stale stamp from the queue is removed.
/// </summary>
public sealed class StackedBaseStampTests
{
    private readonly InMemoryTicketFacts _facts = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new() { CallBase = true }; // 9719d: the set reads loop LatestAsync

    public StackedBaseStampTests() => _facts.Edges.Add(Edge("42", "41"));

    [Fact]
    public async Task ApplyAsync_AGreenPrerequisite_IsStampedAsTheBase()
    {
        Prerequisite(RunStatuses.Success);

        var (request, refusal) = await StackedBaseStamp.ApplyAsync(Request(), Config(RequiresStart.OnGreenPush), Read(), default);

        refusal.Should().BeNull();
        request.InitialContext![ContextKeys.StackedOnTicket].Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["api"] = "41" });
    }

    // 2026-11-23-31dca: the stamp is one base per repo, each repo's nearest prerequisite with work there.
    [Fact]
    public async Task ApplyAsync_APrerequisiteChainAcrossRepos_StampsEachRepo()
    {
        _facts.Edges.Add(Edge("41", "40"));
        Prerequisite(RunStatuses.Success, "server");
        _attempts.Setup(a => a.LatestAsync(Project, "40", null, default))
            .ReturnsAsync(new PreviousAttempt("run-40", RunStatuses.Success, DateTimeOffset.UnixEpoch, Finished: true)
            {
                OpenedPullRequest = true, PullRequestUrls = new Dictionary<string, string> { ["server"] = "https://x/s/40", ["worker"] = "https://x/w/40" },
            });

        var (request, _) = await StackedBaseStamp.ApplyAsync(Request(), Config(RequiresStart.OnGreenPush), Read(), default);

        request.InitialContext![ContextKeys.StackedOnTicket].Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["server"] = "41", ["worker"] = "40" });
    }

    [Fact]
    public async Task ApplyAsync_ARequirementThatTurnedUnmet_IsRefused()
    {
        Prerequisite(RunStatuses.Failed);

        var (_, refusal) = await StackedBaseStamp.ApplyAsync(Request(), Config(RequiresStart.OnGreenPush), Read(), default);

        refusal!.Rejection.Should().Be(ClaimRejectionReason.TicketRequiresUnmet);
        refusal.Error.Should().Contain("#41").And.Contain("green branch");
    }

    [Fact]
    public async Task ApplyAsync_ALandedPrerequisite_RemovesAStaleStamp()
    {
        _facts.Land("41");
        var queued = Request() with { InitialContext = new() { [ContextKeys.StackedOnTicket] = "41", ["Other"] = true } };

        var (request, _) = await StackedBaseStamp.ApplyAsync(queued, Config(RequiresStart.OnGreenPush), Read(), default);

        request.InitialContext.Should().NotContainKey(ContextKeys.StackedOnTicket).And.ContainKey("Other");
    }

    [Fact]
    public async Task ApplyAsync_AnotherPipeline_IsLeftAlone()
    {
        var request = Request() with { PipelineName = "security-scan" };

        var (stamped, refusal) = await StackedBaseStamp.ApplyAsync(request, Config(RequiresStart.OnGreenPush), Read(), default);

        refusal.Should().BeNull();
        stamped.Should().BeSameAs(request);
    }

    private UnmetTicketRequirements Read() =>
        new(_facts, _facts, new InMemorySpecApprovalStore(), new GreenPrerequisites(_facts, _attempts.Object, _facts, _facts));

    private void Prerequisite(string status, string repo = "api") =>
        _attempts.Setup(a => a.LatestAsync(Project, "41", null, default))
            .ReturnsAsync(new PreviousAttempt("run-41", status, DateTimeOffset.UnixEpoch, Finished: true)
            {
                OpenedPullRequest = true, PullRequestUrls = new Dictionary<string, string> { [repo] = $"https://x/{repo}/41" },
            });

    private static ClaimRequest Request() => new("jira", Project, new TicketId("42"), PipelinePresets.CodeName);

    private static AgentSmithConfig Config(RequiresStart mode) => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            [Project] = new() { Name = Project, Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.Jira }, RequiresStart = mode },
        },
    };
}
