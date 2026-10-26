using AgentSmith.Contracts.Commands;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Server.Models;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.NeverRun.NeverRunFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-26-af69: a ticket's summary cost counts its spec derivations, so the list, the Cost &amp; compute tab and
/// the Delivery row show one number; the bucket and the attempts are the code runs' alone.
/// </summary>
public sealed class TicketListDerivationCostTests : IDisposable
{
    private const string Attempt = "2026-10-13T09-00-00-0001";
    private const string Derivation = "2026-10-13T10-00-00-0002";
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TicketList_CodeAttemptAndDerivation_ListsTheirSum_BucketAndAttemptsUnchanged()
    {
        await _h.AddAsync(Run(Attempt, cost: 2m), Step(Attempt));
        var was = (await ListAsync(ProjectScope.Everything)).Single();
        await _h.AddAsync(Run(Derivation, pipeline: PipelinePresets.SpecDeriveName, cost: 1m), Step(Derivation));

        var row = (await ListAsync(ProjectScope.Everything)).Should().ContainSingle().Subject;

        (was.Cost, row.Cost).Should().Be((2m, 3m));
        (row.Bucket, row.AttemptCount, row.NewestRunId).Should().Be((was.Bucket, 1, Attempt));
        (await _h.List.AllAsync(ProjectScope.Everything, default)).Runs.Single().Summary.Cost.Should().Be(3m, "the graph and Delivery read the same summaries");
    }

    [Fact]
    public async Task TicketList_NeverRunFiledTicketWithDerivation_ListsItsCost()
    {
        await _h.AddAsync(Filing("59"), Run(Derivation, ticket: "59", pipeline: PipelinePresets.SpecDeriveName, cost: 1.25m));

        var row = (await ListAsync(ProjectScope.Everything)).Should().ContainSingle().Subject;

        (row.Bucket, row.AttemptCount, row.Cost).Should().Be((TicketBucket.Filed, 0, 1.25m));
    }

    [Fact]
    public async Task TicketList_ScopedCaller_CountsNoDerivationOfAnotherProject()
    {
        await _h.AddAsync(Run(Attempt, cost: 2m), Step(Attempt),
            Run(Derivation, project: "beta", pipeline: PipelinePresets.SpecDeriveName, cost: 5m));

        var row = (await ListAsync(ProjectScope.Of(["alpha"]))).Should().ContainSingle().Subject;

        row.Cost.Should().Be(2m);
    }

    [Fact]
    public async Task TicketDetail_Summary_CostIncludesDerivations()
    {
        await _h.AddAsync(Run(Attempt, cost: 2m), Step(Attempt), Run(Derivation, pipeline: PipelinePresets.SpecDeriveName, cost: 1m));

        var detail = await _h.Detail.ReadAsync("alpha", "42", default);

        detail!.Summary.Cost.Should().Be(3m);
        detail.Attempts.Should().ContainSingle();
    }

    private async Task<IReadOnlyList<TicketSummaryView>> ListAsync(ProjectScope scope) =>
        (await _h.List.ReadAsync(new TicketListRequest(scope, null, null, null), default)).Tickets;
}
