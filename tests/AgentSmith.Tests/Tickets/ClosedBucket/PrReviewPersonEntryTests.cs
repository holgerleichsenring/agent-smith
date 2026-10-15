using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Contracts.Reviews;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Tickets.ClosedBucket.ClosedFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.ClosedBucket;

/// <summary>2026-10-15-114fb: a record's newest person entry, derived on read beside its newest entry.</summary>
public sealed class PrReviewPersonEntryTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task PrReviewRecordRepository_LastPersonEntryAt_SkipsOursBotsAndSystemNotesWithoutADecision()
    {
        await _h.AddAsync(Record("1", PrReviewStates.Open), Record("2", PrReviewStates.Open),
            Entry("1", "a", T.AddHours(1)), Entry("1", "b", T.AddHours(2), ours: true),
            Entry("1", "c", T.AddHours(3), bot: true), Entry("1", "d", T.AddHours(4), system: true),
            Entry("2", "e", T.AddHours(5), system: true, decision: PrReviewDecisions.Approved));

        using var scope = _h.Services.CreateScope();
        var records = await scope.ServiceProvider.GetRequiredService<AgentSmith.Infrastructure.Persistence.Repositories.PrReviewRecordRepository>().ForListAsync(ProjectScope.Single("alpha"), default);

        records.Select(r => (r.Number, r.LastEntryAt, r.LastPersonEntryAt)).Should().Equal(
            ("1", T.AddHours(4), T.AddHours(1)), ("2", T.AddHours(5), T.AddHours(5)));
    }
}
