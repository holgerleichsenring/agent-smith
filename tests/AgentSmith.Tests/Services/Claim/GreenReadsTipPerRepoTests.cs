using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Services.Claim;

/// <summary>
/// 2026-11-23-97c2b: the stack tip per repo — among the unlanded prerequisites of a ticket's chain with a pull request in a
/// repo, the one whose chain reaches every other one with work there. Chain S1 &lt;- S2(server, worker) &lt;- S3(server);
/// the dependent D requires S3 only.
/// </summary>
public sealed class GreenReadsTipPerRepoTests
{
    private readonly InMemoryTicketFacts _facts = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new() { CallBase = true }; // the default set read loops LatestAsync

    public GreenReadsTipPerRepoTests()
    {
        _facts.Edges.AddRange([Edge("s2", "s1"), Edge("s3", "s2"), Edge("d", "s3")]);
        Worked("s1", "server", "worker");
        Worked("s2", "server", "worker");
        Worked("s3", "server");
    }

    [Fact]
    public async Task TipPerRepoAsync_EachRepoStacksOnTheNearestPrerequisiteWithWorkThere()
    {
        var tips = await Reads().TipPerRepoAsync([Edge("d", "s3")], default);

        tips.Should().HaveCount(2);
        tips["server"]!.RequiredTicketId.Should().Be("S3");
        tips["worker"]!.RequiredTicketId.Should().Be("S2", "S3 has no worker branch; S2's carries S1's worker work");
    }

    [Fact]
    public async Task TipPerRepoAsync_ALandedPrerequisite_IsLeftOut()
    {
        _facts.Land("s2");
        _facts.Land("s1");

        var tips = await Reads().TipPerRepoAsync([Edge("d", "s3")], default);

        tips.Keys.Should().Equal("server");
        tips["server"]!.RequiredTicketId.Should().Be("S3");
    }

    [Fact]
    public async Task TipPerRepoAsync_TwoIndependentPrerequisitesWithWorkInOneRepo_HaveNoTipThere()
    {
        _facts.Edges.Add(Edge("d", "x"));
        Worked("x", "worker");

        var tips = await Reads().TipPerRepoAsync([Edge("d", "s3"), Edge("d", "x")], default);

        tips["server"]!.RequiredTicketId.Should().Be("S3");
        tips.Should().ContainKey("worker").WhoseValue.Should().BeNull("X and S2 both worked there and neither carries the other");
    }

    [Fact]
    public async Task UnlandedChainAsync_TheUnmetEdgesFirst_ThenEveryUnlandedPrerequisiteTheyReach()
    {
        _facts.Land("s1");

        var chain = await Reads().UnlandedChainAsync([Edge("d", "s3")], default);

        chain.Select(e => e.RequiredTicketId).Should().Equal("S3", "S2");
    }

    [Fact]
    public async Task ChainAsync_EveryPrerequisiteTheChainReaches_LandedOrNot()
    {
        _facts.Land("s1");

        var chain = await Reads().ChainAsync([Edge("d", "s3")], default);

        chain.Select(e => e.RequiredTicketId).Should().Equal("S3", "S2", "S1");
    }

    private GreenReads Reads() => new GreenPrerequisites(_facts, _attempts.Object, _facts, _facts).Reads(Project);

    private void Worked(string ticket, params string[] repos) =>
        _attempts.Setup(a => a.LatestAsync(Project, ticket.ToUpperInvariant(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt($"run-{ticket}", RunStatuses.Success, DateTimeOffset.UnixEpoch, Finished: true)
            {
                OpenedPullRequest = true,
                PullRequestUrls = repos.ToDictionary(r => r, r => $"https://git.test/o/{r}/pull/{ticket}"),
            });
}
